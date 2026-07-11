import { Spinner } from "@fluentui/react-components";
import { Wrench20Regular } from "@fluentui/react-icons";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { Conversation, MemoryUpdate, ServerSettings, Settings, ToolCall } from "../types";

export interface StreamedVisuals {
  text: string;
  reasoning: string;
  toolCalls: ToolCall[];
}

interface MessageListProps {
  conversation: Conversation;
  settings: Settings;
  serverSettings: ServerSettings;
  isSending: boolean;
  streamed: StreamedVisuals;
  memoryUpdate: MemoryUpdate | null;
  onPreviewImage: (url: string, alt: string) => void;
}

export function MessageList({
  conversation,
  settings,
  serverSettings,
  isSending,
  streamed,
  memoryUpdate,
  onPreviewImage,
}: MessageListProps) {
  const showReasoning = serverSettings.allowChatVisualsCustomization && settings.showReasoning;
  const showTokens = serverSettings.allowChatVisualsCustomization && settings.showTokens;
  const showToolCalls = serverSettings.allowChatVisualsCustomization && settings.showToolCalls;
  const showMemoryUpdate =
    serverSettings.useUserMemory && serverSettings.allowChatVisualsCustomization && settings.showMemoryUpdate;
  const hasStreamedVisuals =
    (showReasoning && streamed.reasoning.length > 0) ||
    streamed.text.length > 0 ||
    (showToolCalls && streamed.toolCalls.length > 0);

  return (
    <div className="message-list">
      {conversation.messages.map((message, index) => {
        if (message.role === "user") {
          return (
            <article className="message-row from-user" key={index}>
              <div className="user-message-stack">
                {message.attachments.length > 0 && (
                  <div className="message-attachments">
                    {message.attachments.map((attachment) => (
                      <div className="attachment-thumbnail" title={attachment.fileName} key={attachment.url}>
                        {attachment.contentType.startsWith("image/") ? (
                          <button
                            type="button"
                            className="chat-image-button attachment-image-button"
                            onClick={() => onPreviewImage(attachment.url, attachment.fileName)}
                          >
                            <img src={attachment.url} alt={attachment.fileName} />
                          </button>
                        ) : (
                          <span>PDF</span>
                        )}
                      </div>
                    ))}
                  </div>
                )}
                <div className="message-bubble user-bubble">{message.text}</div>
              </div>
            </article>
          );
        }

        if (message.role !== "assistant") {
          return null;
        }

        return (
          <div className="assistant-message-group" key={index}>
            {showReasoning &&
              message.reasoning.map((reasoning, reasoningIndex) => (
                <article className="message-row from-assistant" key={`reasoning-${reasoningIndex}`}>
                  <div className="meta-info markdown-content">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{reasoning}</ReactMarkdown>
                  </div>
                </article>
              ))}
            {showToolCalls && message.toolCalls.map((toolCall) => <ToolCallDisplay toolCall={toolCall} key={toolCall.callId} />)}
            {message.text.trim().length > 0 && (
              <article className="message-row from-assistant">
                <div className="message-bubble assistant-bubble">
                  <div className="markdown-content">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{message.text}</ReactMarkdown>
                  </div>
                  {showTokens && message.usage && (
                    <div className="token-usage" aria-label="Token usage">
                      <span>{message.usage.input ?? 0} in</span>
                      <span>{message.usage.output ?? 0} out</span>
                      {(message.usage.reasoning ?? 0) > 0 && <span>{message.usage.reasoning} reasoning</span>}
                    </div>
                  )}
                </div>
              </article>
            )}
            {serverSettings.useImageGeneration && message.imageUrl && (
              <article className="message-row from-assistant">
                <button
                  type="button"
                  className="chat-image-button assistant-image-button"
                  onClick={() => onPreviewImage(message.imageUrl!, "Generated image")}
                >
                  <img src={message.imageUrl} alt="Generated" />
                </button>
              </article>
            )}
          </div>
        );
      })}

      {isSending && !hasStreamedVisuals && (
        <article className="message-row from-assistant">
          <div className="assistant-progress"><Spinner size="tiny" label="Waiting for assistant" /></div>
        </article>
      )}
      {showReasoning && streamed.reasoning.length > 0 && (
        <article className="message-row from-assistant">
          <div className="meta-info markdown-content">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{streamed.reasoning}</ReactMarkdown>
          </div>
        </article>
      )}
      {showToolCalls && streamed.toolCalls.map((toolCall) => <ToolCallDisplay toolCall={toolCall} key={toolCall.callId} />)}
      {streamed.text.length > 0 && (
        <article className="message-row from-assistant">
          <div className="message-bubble assistant-bubble streaming-text">{streamed.text}</div>
        </article>
      )}
      {showMemoryUpdate && memoryUpdate && (
        <article className="message-row from-assistant">
          <p className="memory-update">{memoryUpdate.displayText}</p>
        </article>
      )}
    </div>
  );
}

function ToolCallDisplay({ toolCall }: { toolCall: ToolCall }) {
  return (
    <article className="message-row from-assistant">
      <details className="tool-call-entry">
        <summary>
          <Wrench20Regular />
          <strong>Tool Call</strong>
          <span>{toolCall.summary}</span>
          {!toolCall.result && <Spinner size="extra-tiny" />}
        </summary>
        {toolCall.fullRequest && (
          <div className="tool-call-request">
            <strong>Full Request</strong>
            <p>{toolCall.fullRequest}</p>
          </div>
        )}
        {toolCall.result && <pre className="tool-call-result"><code>{toolCall.result}</code></pre>}
      </details>
    </article>
  );
}
