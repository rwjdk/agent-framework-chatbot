import { useEffect, useState } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Divider,
  Input,
  Label,
  MessageBar,
  MessageBarBody,
  Switch,
  Textarea,
} from "@fluentui/react-components";
import {
  Add20Regular,
  Delete20Regular,
  Dismiss20Regular,
  Save20Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import type { McpServer, ServerSettings, Settings } from "../types";

type SettingsSection = "chat" | "instructions" | "memories" | "integrations" | "danger";

interface SettingsDialogProps {
  open: boolean;
  settings: Settings;
  serverSettings: ServerSettings;
  onClose: () => void;
  onSave: (settings: Settings) => Promise<void>;
  onDeleteData: () => Promise<void>;
}

export function SettingsDialog({
  open,
  settings,
  serverSettings,
  onClose,
  onSave,
  onDeleteData,
}: SettingsDialogProps) {
  const [draft, setDraft] = useState<Settings>(() => cloneSettings(settings));
  const [section, setSection] = useState<SettingsSection>("chat");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => {
    if (open) {
      setDraft(cloneSettings(settings));
      setSection(firstAvailableSection(serverSettings));
      setError(null);
    }
  }, [open, settings, serverSettings]);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await onSave(draft);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Failed to save settings.");
    } finally {
      setSaving(false);
    }
  };

  const updateMcpServer = (index: number, update: Partial<McpServer>) => {
    setDraft((current) => ({
      ...current,
      mcpServers: current.mcpServers.map((server, serverIndex) =>
        serverIndex === index ? { ...server, ...update } : server,
      ),
    }));
  };

  return (
    <>
      <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
        <DialogSurface className="settings-surface">
          <DialogBody>
            <DialogTitle
              action={
                <Button appearance="subtle" icon={<Dismiss20Regular />} aria-label="Close settings" onClick={onClose} />
              }
            >
              Settings
            </DialogTitle>
            <DialogContent className="settings-layout">
              <aside className="settings-navigation">
                {serverSettings.allowChatVisualsCustomization && (
                  <SettingsNavigationButton label="Chat" section="chat" selected={section} onSelect={setSection} />
                )}
                {serverSettings.allowCustomInstructions && (
                  <SettingsNavigationButton
                    label="Instructions"
                    section="instructions"
                    selected={section}
                    onSelect={setSection}
                  />
                )}
                {serverSettings.useUserMemory && (
                  <SettingsNavigationButton
                    label="Memories"
                    section="memories"
                    selected={section}
                    onSelect={setSection}
                  />
                )}
                {serverSettings.allowMcpServers && (
                  <SettingsNavigationButton
                    label="Integrations"
                    section="integrations"
                    selected={section}
                    onSelect={setSection}
                  />
                )}
                <Button
                  appearance={section === "danger" ? "primary" : "subtle"}
                  icon={<Warning20Regular />}
                  onClick={() => setSection("danger")}
                >
                  Danger Zone
                </Button>
              </aside>

              <section className="settings-content">
                {error && (
                  <MessageBar intent="error">
                    <MessageBarBody>{error}</MessageBarBody>
                  </MessageBar>
                )}

                {section === "chat" && (
                  <div className="settings-section">
                    <h2>Chat</h2>
                    <Switch
                      label="Streaming"
                      checked={draft.streaming}
                      onChange={(_, data) => setDraft({ ...draft, streaming: data.checked })}
                    />
                    <Switch
                      label="Show reasoning"
                      checked={draft.showReasoning}
                      onChange={(_, data) => setDraft({ ...draft, showReasoning: data.checked })}
                    />
                    <Switch
                      label="Show tokens"
                      checked={draft.showTokens}
                      onChange={(_, data) => setDraft({ ...draft, showTokens: data.checked })}
                    />
                    <Switch
                      label="Show tool calls"
                      checked={draft.showToolCalls}
                      onChange={(_, data) => setDraft({ ...draft, showToolCalls: data.checked })}
                    />
                    {serverSettings.useUserMemory && (
                      <Switch
                        label="Show memory updates"
                        checked={draft.showMemoryUpdate}
                        onChange={(_, data) => setDraft({ ...draft, showMemoryUpdate: data.checked })}
                      />
                    )}
                    <SaveButton disabled={saving} onClick={save} />
                  </div>
                )}

                {section === "instructions" && (
                  <div className="settings-section">
                    <h2>Instructions</h2>
                    <Label htmlFor="custom-instructions">Custom Instructions</Label>
                    <Textarea
                      id="custom-instructions"
                      value={draft.instructions}
                      resize="vertical"
                      onChange={(_, data) => setDraft({ ...draft, instructions: data.value })}
                    />
                    <SaveButton disabled={saving} onClick={save} />
                  </div>
                )}

                {section === "memories" && (
                  <div className="settings-section">
                    <div className="settings-section-heading">
                      <h2>Memories</h2>
                      <Button
                        appearance="primary"
                        icon={<Add20Regular />}
                        onClick={() => setDraft({ ...draft, userMemories: [...draft.userMemories, ""] })}
                      >
                        Memory
                      </Button>
                    </div>
                    {draft.userMemories.length === 0 && (
                      <MessageBar>
                        <MessageBarBody>No memories saved.</MessageBarBody>
                      </MessageBar>
                    )}
                    {draft.userMemories.map((memory, index) => (
                      <div className="memory-row" key={index}>
                        <Textarea
                          value={memory}
                          resize="vertical"
                          aria-label={`Memory ${index + 1}`}
                          onChange={(_, data) =>
                            setDraft({
                              ...draft,
                              userMemories: draft.userMemories.map((value, memoryIndex) =>
                                memoryIndex === index ? data.value : value,
                              ),
                            })
                          }
                        />
                        <Button
                          appearance="subtle"
                          icon={<Delete20Regular />}
                          aria-label={`Remove memory ${index + 1}`}
                          onClick={() =>
                            setDraft({
                              ...draft,
                              userMemories: draft.userMemories.filter((_, memoryIndex) => memoryIndex !== index),
                            })
                          }
                        />
                      </div>
                    ))}
                    <SaveButton disabled={saving} onClick={save} />
                  </div>
                )}

                {section === "integrations" && (
                  <div className="settings-section">
                    <div className="settings-section-heading">
                      <h2>MCP servers</h2>
                      <Button
                        appearance="primary"
                        icon={<Add20Regular />}
                        onClick={() =>
                          setDraft({
                            ...draft,
                            mcpServers: [...draft.mcpServers, { name: "", url: "", headers: {} }],
                          })
                        }
                      >
                        Server
                      </Button>
                    </div>
                    {draft.mcpServers.length === 0 && (
                      <MessageBar>
                        <MessageBarBody>No MCP servers configured.</MessageBarBody>
                      </MessageBar>
                    )}
                    {draft.mcpServers.map((server, serverIndex) => (
                      <div className="mcp-server" key={serverIndex}>
                        <div className="settings-section-heading">
                          <strong>Server {serverIndex + 1}</strong>
                          <Button
                            appearance="subtle"
                            icon={<Delete20Regular />}
                            aria-label={`Remove server ${serverIndex + 1}`}
                            onClick={() =>
                              setDraft({
                                ...draft,
                                mcpServers: draft.mcpServers.filter((_, index) => index !== serverIndex),
                              })
                            }
                          />
                        </div>
                        <Label>Name</Label>
                        <Input value={server.name} onChange={(_, data) => updateMcpServer(serverIndex, { name: data.value })} />
                        <Label>URL</Label>
                        <Input value={server.url} onChange={(_, data) => updateMcpServer(serverIndex, { url: data.value })} />
                        <Divider />
                        <div className="settings-section-heading">
                          <strong>Headers</strong>
                          <Button
                            appearance="secondary"
                            size="small"
                            icon={<Add20Regular />}
                            onClick={() => updateMcpServer(serverIndex, { headers: { ...server.headers, "": "" } })}
                          >
                            Header
                          </Button>
                        </div>
                        {Object.entries(server.headers).map(([key, value], headerIndex) => (
                          <div className="mcp-header-row" key={`${headerIndex}-${key}`}>
                            <Input
                              value={key}
                              placeholder="Key"
                              aria-label={`Header ${headerIndex + 1} key`}
                              onChange={(_, data) => {
                                const entries = Object.entries(server.headers);
                                entries[headerIndex] = [data.value, value];
                                updateMcpServer(serverIndex, { headers: Object.fromEntries(entries) });
                              }}
                            />
                            <Input
                              type="password"
                              value={value}
                              placeholder="Value"
                              aria-label={`Header ${headerIndex + 1} value`}
                              onChange={(_, data) =>
                                updateMcpServer(serverIndex, { headers: { ...server.headers, [key]: data.value } })
                              }
                            />
                            <Button
                              appearance="subtle"
                              icon={<Dismiss20Regular />}
                              aria-label={`Remove header ${headerIndex + 1}`}
                              onClick={() =>
                                updateMcpServer(serverIndex, {
                                  headers: Object.fromEntries(
                                    Object.entries(server.headers).filter((_, index) => index !== headerIndex),
                                  ),
                                })
                              }
                            />
                          </div>
                        ))}
                      </div>
                    ))}
                    <SaveButton disabled={saving} onClick={save} />
                  </div>
                )}

                {section === "danger" && (
                  <div className="settings-section">
                    <h2>Danger Zone</h2>
                    <MessageBar intent="error">
                      <MessageBarBody>This permanently deletes your settings and conversations.</MessageBarBody>
                    </MessageBar>
                    <Button appearance="primary" icon={<Delete20Regular />} onClick={() => setConfirmDelete(true)}>
                      Delete my Settings and Conversations
                    </Button>
                  </div>
                )}
              </section>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>Close</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={confirmDelete} onOpenChange={(_, data) => setConfirmDelete(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Delete settings and conversations?</DialogTitle>
            <DialogContent>This cannot be undone.</DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setConfirmDelete(false)}>Cancel</Button>
              <Button
                appearance="primary"
                onClick={async () => {
                  await onDeleteData();
                  setConfirmDelete(false);
                  onClose();
                }}
              >
                Delete
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
}

function SettingsNavigationButton({
  label,
  section,
  selected,
  onSelect,
}: {
  label: string;
  section: SettingsSection;
  selected: SettingsSection;
  onSelect: (section: SettingsSection) => void;
}) {
  return (
    <Button appearance={selected === section ? "primary" : "subtle"} onClick={() => onSelect(section)}>
      {label}
    </Button>
  );
}

function SaveButton({ disabled, onClick }: { disabled: boolean; onClick: () => void }) {
  return (
    <Button appearance="primary" icon={<Save20Regular />} disabled={disabled} onClick={onClick}>
      Save
    </Button>
  );
}

function firstAvailableSection(serverSettings: ServerSettings): SettingsSection {
  if (serverSettings.allowChatVisualsCustomization) {
    return "chat";
  }
  if (serverSettings.allowCustomInstructions) {
    return "instructions";
  }
  if (serverSettings.useUserMemory) {
    return "memories";
  }
  if (serverSettings.allowMcpServers) {
    return "integrations";
  }
  return "danger";
}

function cloneSettings(settings: Settings): Settings {
  return {
    ...settings,
    userMemories: [...settings.userMemories],
    mcpServers: settings.mcpServers.map((server) => ({ ...server, headers: { ...server.headers } })),
  };
}
