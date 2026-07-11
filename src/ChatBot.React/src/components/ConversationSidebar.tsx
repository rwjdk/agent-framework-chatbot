import {
  Button,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
} from "@fluentui/react-components";
import {
  Edit20Regular,
  MoreHorizontal20Regular,
  Settings20Regular,
  SignOut20Regular,
} from "@fluentui/react-icons";
import type { ConversationSummary } from "../types";

interface ConversationSidebarProps {
  conversations: ConversationSummary[];
  activeConversationId?: string;
  displayName: string;
  disabled: boolean;
  onNewChat: () => void;
  onSelect: (conversation: ConversationSummary) => void;
  onRename: (conversation: ConversationSummary) => void;
  onDelete: (conversation: ConversationSummary) => void;
  onSettings: () => void;
  onLogout: () => void;
}

export function ConversationSidebar({
  conversations,
  activeConversationId,
  displayName,
  disabled,
  onNewChat,
  onSelect,
  onRename,
  onDelete,
  onSettings,
  onLogout,
}: ConversationSidebarProps) {
  return (
    <aside className="conversation-sidebar">
      <Button appearance="primary" icon={<Edit20Regular />} disabled={disabled} onClick={onNewChat}>
        New Chat
      </Button>

      <div className="conversation-list">
        <div className="conversation-list-header">Recents</div>
        {conversations.map((conversation) => (
          <div
            className={`conversation-row ${conversation.id === activeConversationId ? "active" : ""}`}
            key={conversation.id}
          >
            <button
              className="conversation-select-button"
              type="button"
              disabled={disabled}
              onClick={() => onSelect(conversation)}
            >
              {conversation.title ?? "New conversation"}
            </button>
            <Menu>
              <MenuTrigger disableButtonEnhancement>
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<MoreHorizontal20Regular />}
                  disabled={disabled}
                  aria-label="Conversation actions"
                  title="Conversation actions"
                />
              </MenuTrigger>
              <MenuPopover>
                <MenuList>
                  <MenuItem onClick={() => onRename(conversation)}>Rename</MenuItem>
                  <MenuItem onClick={() => onDelete(conversation)}>Delete</MenuItem>
                </MenuList>
              </MenuPopover>
            </Menu>
          </div>
        ))}
      </div>

      <div className="sidebar-footer">
        <div className="signed-in-user">
          <span>Signed in as</span>
          <strong title={displayName}>{displayName}</strong>
        </div>
        <div className="sidebar-footer-actions">
          <Button
            appearance="subtle"
            size="small"
            icon={<Settings20Regular />}
            disabled={disabled}
            aria-label="Settings"
            title="Settings"
            onClick={onSettings}
          />
          <Button
            appearance="subtle"
            size="small"
            icon={<SignOut20Regular />}
            disabled={disabled}
            aria-label="Logout"
            title="Logout"
            onClick={onLogout}
          />
        </div>
      </div>
    </aside>
  );
}
