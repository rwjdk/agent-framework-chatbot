import { useEffect, useRef, useState } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Input,
  Label,
  MessageBar,
  MessageBarBody,
  Spinner,
} from "@fluentui/react-components";
import { Dismiss20Regular } from "@fluentui/react-icons";
import {
  ApiError,
  createConversation,
  deleteConversation,
  deleteUserData,
  getConversation,
  getConversations,
  getServerSettings,
  getSession,
  getSettings,
  renameConversation,
  saveSettings,
  sendMessage,
  transcribeAudio,
} from "./api";
import { Composer } from "./components/Composer";
import { ConversationSidebar } from "./components/ConversationSidebar";
import { MessageList, type StreamedVisuals } from "./components/MessageList";
import { SettingsDialog } from "./components/SettingsDialog";
import type {
  Conversation,
  ConversationSummary,
  MemoryUpdate,
  ServerSettings,
  Settings,
  UserSession,
} from "./types";

type AuthenticationState = "loading" | "authenticated" | "unauthenticated";

const emptyStreamedVisuals: StreamedVisuals = { text: "", reasoning: "", toolCalls: [] };

export default function App() {
  const [authenticationState, setAuthenticationState] = useState<AuthenticationState>("loading");
  const [session, setSession] = useState<UserSession | null>(null);
  const [serverSettings, setServerSettings] = useState<ServerSettings | null>(null);
  const [settings, setSettings] = useState<Settings | null>(null);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [conversation, setConversation] = useState<Conversation | null>(null);
  const [message, setMessage] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [isSending, setIsSending] = useState(false);
  const [streamed, setStreamed] = useState<StreamedVisuals>(emptyStreamedVisuals);
  const [memoryUpdate, setMemoryUpdate] = useState<MemoryUpdate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [renameTarget, setRenameTarget] = useState<ConversationSummary | null>(null);
  const [renameTitle, setRenameTitle] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<ConversationSummary | null>(null);
  const [logoutOpen, setLogoutOpen] = useState(false);
  const [imagePreview, setImagePreview] = useState<{ url: string; alt: string } | null>(null);
  const messagesRef = useRef<HTMLElement>(null);

  useEffect(() => {
    let cancelled = false;
    const initializeAsync = async () => {
      try {
        const loadedSession = await getSession();
        const [loadedServerSettings, loadedSettings, loadedConversations, newConversation] = await Promise.all([
          getServerSettings(),
          getSettings(),
          getConversations(),
          createConversation(),
        ]);
        if (cancelled) {
          return;
        }

        setSession(loadedSession);
        setServerSettings(loadedServerSettings);
        setSettings(loadedSettings);
        setConversations(loadedConversations);
        setConversation(newConversation);
        setAuthenticationState("authenticated");
      } catch (initializationError) {
        if (cancelled) {
          return;
        }

        if (initializationError instanceof ApiError && initializationError.status === 401) {
          setAuthenticationState("unauthenticated");
        } else {
          setError(initializationError instanceof Error ? initializationError.message : "Failed to load the chatbot.");
          setAuthenticationState("unauthenticated");
        }
      }
    };

    void initializeAsync();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    messagesRef.current?.scrollTo({ top: messagesRef.current.scrollHeight, behavior: "smooth" });
  }, [conversation, streamed, memoryUpdate]);

  const startNewChat = async () => {
    try {
      setConversation(await createConversation());
      setMessage("");
      setFiles([]);
      setMemoryUpdate(null);
      setStreamed(emptyStreamedVisuals);
    } catch (newChatError) {
      showError(newChatError);
    }
  };

  const selectConversation = async (summary: ConversationSummary) => {
    try {
      setConversation(await getConversation(summary.id));
      setMessage("");
      setFiles([]);
      setMemoryUpdate(null);
      setStreamed(emptyStreamedVisuals);
    } catch (selectionError) {
      showError(selectionError);
    }
  };

  const submitMessage = async () => {
    const trimmedMessage = message.trim();
    if (!conversation || trimmedMessage.length === 0 || isSending) {
      return;
    }

    const submittedFiles = files;
    let serverAcceptedMessage = false;
    setIsSending(true);
    setError(null);
    setMessage("");
    setFiles([]);
    setMemoryUpdate(null);
    setStreamed(emptyStreamedVisuals);
    try {
      await sendMessage(conversation.id, trimmedMessage, submittedFiles, (event) => {
        if (event.type === "conversation" && event.conversation) {
          serverAcceptedMessage = true;
          setConversation(event.conversation);
          upsertConversationSummary(event.conversation);
        } else if (event.type === "reasoning" && event.text) {
          setStreamed((current) => ({ ...current, reasoning: current.reasoning + event.text }));
        } else if (event.type === "text" && event.text) {
          setStreamed((current) => ({ ...current, text: current.text + event.text }));
        } else if (event.type === "toolCall" && event.toolCall) {
          setStreamed((current) => ({
            ...current,
            toolCalls: [...current.toolCalls.filter((tool) => tool.callId !== event.toolCall!.callId), event.toolCall!],
          }));
        } else if (event.type === "memory" && event.memoryUpdate) {
          setMemoryUpdate(event.memoryUpdate);
        } else if (event.type === "complete" && event.conversation) {
          setConversation(event.conversation);
          upsertConversationSummary(event.conversation);
          setStreamed(emptyStreamedVisuals);
        } else if (event.type === "error") {
          throw new Error(event.error ?? "The assistant failed to respond.");
        }
      });
    } catch (sendError) {
      if (!serverAcceptedMessage) {
        setMessage(trimmedMessage);
        setFiles(submittedFiles);
      }
      showError(sendError);
    } finally {
      setIsSending(false);
    }
  };

  const confirmRename = async () => {
    if (!renameTarget || renameTitle.trim().length === 0) {
      return;
    }

    try {
      const renamed = await renameConversation(renameTarget.id, renameTitle.trim());
      setConversations((current) => current.map((item) => (item.id === renamed.id ? renamed : item)));
      setConversation((current) => (current?.id === renamed.id ? { ...current, title: renamed.title } : current));
      setRenameTarget(null);
    } catch (renameError) {
      showError(renameError);
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) {
      return;
    }

    try {
      await deleteConversation(deleteTarget.id);
      setConversations((current) => current.filter((item) => item.id !== deleteTarget.id));
      if (conversation?.id === deleteTarget.id) {
        await startNewChat();
      }
      setDeleteTarget(null);
    } catch (deleteError) {
      showError(deleteError);
    }
  };

  const saveCurrentSettings = async (nextSettings: Settings) => {
    const saved = await saveSettings(nextSettings);
    setSettings(saved);
  };

  const deleteAllUserData = async () => {
    await deleteUserData();
    const [defaultSettings, newConversation] = await Promise.all([getSettings(), createConversation()]);
    setSettings(defaultSettings);
    setConversations([]);
    setConversation(newConversation);
    setMemoryUpdate(null);
    setStreamed(emptyStreamedVisuals);
  };

  const upsertConversationSummary = (updatedConversation: Conversation) => {
    if (!updatedConversation.title) {
      return;
    }

    setConversations((current) => [
      { id: updatedConversation.id, title: updatedConversation.title },
      ...current.filter((item) => item.id !== updatedConversation.id),
    ]);
  };

  const showError = (caughtError: unknown) => {
    setError(caughtError instanceof Error ? caughtError.message : "Something went wrong.");
  };

  if (authenticationState === "loading") {
    return <div className="centered-state"><Spinner size="large" label="Loading chatbot" /></div>;
  }

  if (
    authenticationState === "unauthenticated" ||
    !session ||
    !serverSettings ||
    !settings ||
    !conversation
  ) {
    return (
      <main className="sign-in-page">
        <div className="sign-in-card">
          <h1>Chatbot</h1>
          <p>Sign in to open the React and Fluent UI chatbot.</p>
          {error && (
            <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>
          )}
          <Button appearance="primary" as="a" href="/api/auth/login?returnUrl=/">Sign in</Button>
        </div>
      </main>
    );
  }

  return (
    <main className="chat-page">
      <ConversationSidebar
        conversations={conversations}
        activeConversationId={conversation.id}
        displayName={session.displayName}
        disabled={isSending}
        onNewChat={() => void startNewChat()}
        onSelect={(summary) => void selectConversation(summary)}
        onRename={(summary) => {
          setRenameTarget(summary);
          setRenameTitle(summary.title ?? "");
        }}
        onDelete={setDeleteTarget}
        onSettings={() => setSettingsOpen(true)}
        onLogout={() => setLogoutOpen(true)}
      />

      <div className="chat-main">
        {error && (
          <MessageBar className="app-error" intent="error">
            <MessageBarBody>{error}</MessageBarBody>
            <Button appearance="transparent" icon={<Dismiss20Regular />} aria-label="Dismiss error" onClick={() => setError(null)} />
          </MessageBar>
        )}
        <section className="chat-messages" ref={messagesRef}>
          <MessageList
            conversation={conversation}
            settings={settings}
            serverSettings={serverSettings}
            isSending={isSending}
            streamed={streamed}
            memoryUpdate={memoryUpdate}
            onPreviewImage={(url, alt) => setImagePreview({ url, alt })}
          />
        </section>
        <Composer
          text={message}
          files={files}
          disabled={isSending}
          allowAttachments={serverSettings.allowFileAttachments}
          allowAudio={serverSettings.allowAudioTranscription}
          onTextChange={setMessage}
          onFilesChange={setFiles}
          onSend={() => void submitMessage()}
          onTranscribe={transcribeAudio}
          onError={setError}
        />
      </div>

      <SettingsDialog
        open={settingsOpen}
        settings={settings}
        serverSettings={serverSettings}
        onClose={() => setSettingsOpen(false)}
        onSave={saveCurrentSettings}
        onDeleteData={deleteAllUserData}
      />

      <Dialog open={renameTarget !== null} onOpenChange={(_, data) => !data.open && setRenameTarget(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Rename conversation</DialogTitle>
            <DialogContent className="simple-dialog-content">
              <Label htmlFor="conversation-title">Title</Label>
              <Input id="conversation-title" value={renameTitle} onChange={(_, data) => setRenameTitle(data.value)} />
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setRenameTarget(null)}>Cancel</Button>
              <Button appearance="primary" disabled={renameTitle.trim().length === 0} onClick={() => void confirmRename()}>Rename</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={deleteTarget !== null} onOpenChange={(_, data) => !data.open && setDeleteTarget(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Delete conversation?</DialogTitle>
            <DialogContent>Delete “{deleteTarget?.title}”? This cannot be undone.</DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setDeleteTarget(null)}>Cancel</Button>
              <Button appearance="primary" onClick={() => void confirmDelete()}>Delete</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={logoutOpen} onOpenChange={(_, data) => setLogoutOpen(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Logout?</DialogTitle>
            <DialogContent>Are you sure you want to logout?</DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setLogoutOpen(false)}>Cancel</Button>
              <Button appearance="primary" onClick={() => { window.location.href = "/api/auth/logout"; }}>Logout</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={imagePreview !== null} onOpenChange={(_, data) => !data.open && setImagePreview(null)}>
        <DialogSurface className="image-preview-surface">
          <DialogBody>
            <DialogTitle
              action={<Button appearance="subtle" icon={<Dismiss20Regular />} aria-label="Close preview" onClick={() => setImagePreview(null)} />}
            >
              {imagePreview?.alt}
            </DialogTitle>
            <DialogContent className="image-preview-content">
              {imagePreview && <img src={imagePreview.url} alt={imagePreview.alt} />}
            </DialogContent>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </main>
  );
}
