import React, { useState } from 'react';
import { Send, Bot, User, Loader2 } from 'lucide-react';
import { sendChatMessage } from '../Services/Api';

interface Message {
  id: string;
  sender: 'user' | 'bot';
  text: string;
  timestamp: string;
}

export const Chat: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [question, setQuestion] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!question.trim() || isLoading) return;

    const userMsg: Message = {
      id: Math.random().toString(),
      sender: 'user',
      text: question,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };

    setMessages((prev) => [...prev, userMsg]);
    const currentQuestion = question;
    setQuestion('');
    setIsLoading(true);

    try {
      const answer = await sendChatMessage(currentQuestion);

      const botMsg: Message = {
        id: Math.random().toString(),
        sender: 'bot',
        text: answer || 'Não encontrei informações correspondentes nos documentos.',
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      };

      setMessages((prev) => [...prev, botMsg]);
    } catch (error: any) {
  setMessages((prev) => [
    ...prev,
    {
      id: Math.random().toString(),
      sender: 'bot',
      // Exibe a mensagem real de erro capturada da API
      text: `Erro: ${error.message || 'Falha ao conectar com o servidor.'}`,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    },
  ]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <Bot style={{ color: '#0969da' }} size={22} />
        <h3 style={styles.title}>Chat Inteligente (AWS Bedrock)</h3>
      </div>

      <div style={styles.messageList}>
        {messages.length === 0 ? (
          <div style={styles.emptyState}>
            <Bot size={48} style={{ color: '#8c959f', marginBottom: '12px' }} />
            <p style={{ margin: 0, fontWeight: 600 }}>Faça uma pergunta sobre os arquivos do sistema</p>
            <span style={{ fontSize: '13px', color: '#6e7781', marginTop: '4px' }}>
              Exemplo: "Quais são as informações extraídas dos certificados?"
            </span>
          </div>
        ) : (
          messages.map((msg) => (
            <div
              key={msg.id}
              style={{
                ...styles.messageRow,
                justifyContent: msg.sender === 'user' ? 'flex-end' : 'flex-start',
              }}
            >
              <div
                style={{
                  ...styles.bubble,
                  backgroundColor: msg.sender === 'user' ? '#0969da' : '#f6f8fa',
                  color: msg.sender === 'user' ? '#ffffff' : '#24292e',
                  border: msg.sender === 'user' ? 'none' : '1px solid #d0d7de',
                }}
              >
                <div style={styles.bubbleHeader}>
                  {msg.sender === 'user' ? <User size={14} /> : <Bot size={14} />}
                  <span style={{ fontSize: '11px', opacity: 0.8 }}>{msg.timestamp}</span>
                </div>
                <p style={styles.messageText}>{msg.text}</p>
              </div>
            </div>
          ))
        )}

        {isLoading && (
          <div style={{ ...styles.messageRow, justifyContent: 'flex-start' }}>
            <div style={{ ...styles.bubble, backgroundColor: '#f6f8fa', border: '1px solid #d0d7de' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#57606a' }}>
                <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} />
                <span style={{ fontSize: '13px' }}>Analisando documentos via haiku 4.5...</span>
              </div>
            </div>
          </div>
        )}
      </div>

      <form onSubmit={handleSend} style={styles.inputForm}>
        <input
          type="text"
          placeholder="Digite sua dúvida..."
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          disabled={isLoading}
          style={styles.input}
        />
        <button type="submit" disabled={isLoading || !question.trim()} style={styles.sendButton}>
          <Send size={16} />
        </button>
      </form>
    </div>
  );
};

const styles: { [key: string]: React.CSSProperties } = {
  container: {
    backgroundColor: '#ffffff',
    borderRadius: '12px',
    border: '1px solid #e1e4e8',
    boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
    display: 'flex',
    flexDirection: 'column',
    height: '520px',
  },
  header: {
    padding: '16px',
    borderBottom: '1px solid #e1e4e8',
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
  },
  title: {
    margin: 0,
    fontSize: '16px',
    fontWeight: '600',
    color: '#24292e',
  },
  messageList: {
    flex: 1,
    padding: '16px',
    overflowY: 'auto',
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    color: '#57606a',
  },
  messageRow: {
    display: 'flex',
    width: '100%',
  },
  bubble: {
    maxWidth: '80%',
    padding: '10px 14px',
    borderRadius: '8px',
    fontSize: '14px',
  },
  bubbleHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    marginBottom: '4px',
  },
  messageText: {
    margin: 0,
    lineHeight: '1.4',
    wordBreak: 'break-word',
    whiteSpace: 'pre-wrap',
  },
  inputForm: {
    display: 'flex',
    padding: '12px',
    borderTop: '1px solid #e1e4e8',
    gap: '8px',
  },
  input: {
    flex: 1,
    padding: '10px 12px',
    borderRadius: '6px',
    border: '1px solid #d0d7de',
    fontSize: '14px',
    outline: 'none',
  },
  sendButton: {
    backgroundColor: '#0969da',
    color: '#ffffff',
    border: 'none',
    borderRadius: '6px',
    padding: '0 16px',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
};