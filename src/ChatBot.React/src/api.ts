import type {
  ChatStreamEvent,
  Conversation,
  ConversationSummary,
  ServerSettings,
  Settings,
  UserSession,
} from "./types";

let antiforgeryToken = "";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

export async function getSession(): Promise<UserSession> {
  const session = await request<UserSession>("/api/session");
  antiforgeryToken = session.antiforgeryToken;
  return session;
}

export function getServerSettings(): Promise<ServerSettings> {
  return request<ServerSettings>("/api/server-settings");
}

export function getConversations(): Promise<ConversationSummary[]> {
  return request<ConversationSummary[]>("/api/conversations");
}

export function createConversation(): Promise<Conversation> {
  return request<Conversation>("/api/conversations", mutation("POST"));
}

export function getConversation(conversationId: string): Promise<Conversation> {
  return request<Conversation>(`/api/conversations/${conversationId}`);
}

export function renameConversation(conversationId: string, title: string): Promise<ConversationSummary> {
  return request<ConversationSummary>(
    `/api/conversations/${conversationId}/title`,
    mutation("PUT", JSON.stringify({ title }), true),
  );
}

export async function deleteConversation(conversationId: string): Promise<void> {
  await request<void>(`/api/conversations/${conversationId}`, mutation("DELETE"));
}

export function getSettings(): Promise<Settings> {
  return request<Settings>("/api/settings");
}

export function saveSettings(settings: Settings): Promise<Settings> {
  return request<Settings>("/api/settings", mutation("PUT", JSON.stringify(settings), true));
}

export async function deleteUserData(): Promise<void> {
  await request<void>("/api/data", mutation("DELETE"));
}

export async function transcribeAudio(audio: Blob, fileName: string): Promise<string> {
  const formData = new FormData();
  formData.append("audio", audio, fileName);
  const result = await request<{ text: string }>("/api/transcriptions", mutation("POST", formData));
  return result.text;
}

export async function sendMessage(
  conversationId: string,
  message: string,
  files: File[],
  onEvent: (event: ChatStreamEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  const formData = new FormData();
  formData.append("message", message);
  files.forEach((file) => formData.append("files", file, file.name));

  const response = await fetch(`/api/conversations/${conversationId}/messages`, {
    ...mutation("POST", formData),
    signal,
  });
  if (!response.ok) {
    throw await toApiError(response);
  }

  if (!response.body) {
    throw new ApiError("The server returned an empty chat response.", response.status);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let pending = "";
  while (true) {
    const readResult = await reader.read();
    pending += decoder.decode(readResult.value, { stream: !readResult.done });
    const lines = pending.split("\n");
    pending = lines.pop() ?? "";
    lines.filter((line) => line.trim().length > 0).forEach((line) => onEvent(JSON.parse(line) as ChatStreamEvent));
    if (readResult.done) {
      break;
    }
  }

  if (pending.trim().length > 0) {
    onEvent(JSON.parse(pending) as ChatStreamEvent);
  }
}

function mutation(method: string, body?: BodyInit, json = false): RequestInit {
  const headers = new Headers({ "X-XSRF-TOKEN": antiforgeryToken });
  if (json) {
    headers.set("Content-Type", "application/json");
  }

  return {
    method,
    body,
    headers,
    credentials: "same-origin",
  };
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { credentials: "same-origin", ...init });
  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  let message = `Request failed (${response.status}).`;
  try {
    const problem = (await response.json()) as { detail?: string; title?: string };
    message = problem.detail ?? problem.title ?? message;
  } catch {
    // The fallback message already contains the useful status code.
  }

  return new ApiError(message, response.status);
}
