export interface UserSession {
  displayName: string;
  antiforgeryToken: string;
}

export interface ServerSettings {
  useImageGeneration: boolean;
  useUserMemory: boolean;
  allowMcpServers: boolean;
  allowCustomInstructions: boolean;
  allowChatVisualsCustomization: boolean;
  allowFileAttachments: boolean;
  allowAudioTranscription: boolean;
}

export interface ConversationSummary {
  id: string;
  title: string | null;
}

export interface Conversation extends ConversationSummary {
  messages: ConversationMessage[];
}

export interface ConversationMessage {
  role: "user" | "assistant" | "system";
  text: string;
  attachments: ConversationAttachment[];
  reasoning: string[];
  toolCalls: ToolCall[];
  usage: TokenUsage | null;
  imageUrl: string | null;
}

export interface ConversationAttachment {
  fileName: string;
  contentType: string;
  url: string;
}

export interface ToolCall {
  callId: string;
  summary: string;
  fullRequest: string | null;
  result: string | null;
}

export interface TokenUsage {
  input: number | null;
  output: number | null;
  reasoning: number | null;
}

export interface Settings {
  streaming: boolean;
  showReasoning: boolean;
  showTokens: boolean;
  showToolCalls: boolean;
  showMemoryUpdate: boolean;
  instructions: string;
  userMemories: string[];
  mcpServers: McpServer[];
}

export interface McpServer {
  name: string;
  url: string;
  headers: Record<string, string>;
}

export interface MemoryUpdate {
  added: string[];
  removed: string[];
  displayText: string;
}

export interface ChatStreamEvent {
  type: "conversation" | "reasoning" | "text" | "toolCall" | "memory" | "complete" | "error";
  conversation?: Conversation;
  text?: string;
  toolCall?: ToolCall;
  memoryUpdate?: MemoryUpdate;
  error?: string;
}
