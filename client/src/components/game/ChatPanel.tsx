import React, { useEffect, useRef, useState } from 'react';
import useAppDispatch from '../../hooks/useAppDispatch';
import useAppSelector from '../../hooks/useAppSelector';
import {
  sendChatMessageAsync,
  loadChatHistoryAsync,
  clearChatBlocked,
} from '../../store/slices/gameSlice';
import styles from './ChatPanel.module.css';

interface Props {
  sessionId: string;
  token: string;
}

const VIOLATION_WARNING_THRESHOLD = 3;

const ChatPanel: React.FC<Props> = ({ sessionId, token }) => {
  const dispatch = useAppDispatch();
  const { gameMessages, chatBlocked } = useAppSelector((s) => s.game);
  const { user } = useAppSelector((s) => s.auth);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    dispatch(loadChatHistoryAsync({ sessionId, token }));
  }, [sessionId, token]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [gameMessages]);

  const handleSend = async () => {
    const text = input.trim();
    if (!text || sending) return;
    setSending(true);
    setInput('');
    await dispatch(sendChatMessageAsync({ sessionId, message: text, token }));
    setSending(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const blockedWarning = chatBlocked
    ? chatBlocked.violationCount >= VIOLATION_WARNING_THRESHOLD
      ? `Restricted: ${chatBlocked.reason}`
      : `Blocked: ${chatBlocked.reason}`
    : null;

  return (
    <div className={styles.panel}>
      <div className={styles.header}>Chat</div>

      <div className={styles.messages}>
        {gameMessages.map((msg) => (
          <div
            key={msg.id}
            className={[
              styles.message,
              msg.userId === user?.id ? styles.mine : '',
            ].join(' ')}
          >
            <span className={styles.username}>{msg.username}</span>
            <span className={styles.text}>{msg.message}</span>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>

      {blockedWarning && (
        <div className={styles.blocked}>
          <span>{blockedWarning}</span>
          <button
            className={styles.blockedClose}
            onClick={() => dispatch(clearChatBlocked())}
          >
            ✕
          </button>
        </div>
      )}

      <div className={styles.inputRow}>
        <input
          className={styles.input}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Say something…"
          maxLength={500}
          disabled={sending}
        />
        <button
          className={styles.sendBtn}
          onClick={handleSend}
          disabled={!input.trim() || sending}
        >
          Send
        </button>
      </div>
    </div>
  );
};

export default ChatPanel;
