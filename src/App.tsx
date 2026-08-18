import { useState } from 'react';
import { FileUpload } from './Components/FileUpload';
import { Chat } from './Components/Chat'; // Certifique-se de que o caminho do arquivo está correto

function App() {
  const [activeTab, setActiveTab] = useState<'upload' | 'chat'>('upload');

  const handleUploadSuccess = () => {
    console.log('Upload feito com sucesso! Aqui atualizaremos a lista de arquivos.');
  };

  return (
    <div style={styles.page}>
      <header style={styles.header}>
        <h1 style={styles.title}>Smart Asset Hub</h1>
        <p style={styles.subtitle}>
          Upload, Processamento Inteligente e Análise RAG na AWS
        </p>

        {/* Menu de Navegação por Abas */}
        <nav style={styles.nav}>
          <button
            onClick={() => setActiveTab('upload')}
            style={{
              ...styles.tabButton,
              ...(activeTab === 'upload' ? styles.activeTabButton : {}),
            }}
          >
            Upload de Arquivos
          </button>
          <button
            onClick={() => setActiveTab('chat')}
            style={{
              ...styles.tabButton,
              ...(activeTab === 'chat' ? styles.activeTabButton : {}),
            }}
          >
            Chat Inteligente (haiku 4.5)
          </button>
        </nav>
      </header>

      <main style={styles.main}>
        {activeTab === 'upload' ? (
          <section style={styles.section}>
            <FileUpload onUploadSuccess={handleUploadSuccess} />
          </section>
        ) : (
          <section style={styles.section}>
            <Chat />
          </section>
        )}
      </main>
    </div>
  );
}

const styles: { [key: string]: React.CSSProperties } = {
  page: {
    minHeight: '100vh',
    backgroundColor: '#f6f8fa',
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
    padding: '40px 20px',
  },
  header: {
    maxWidth: '800px',
    margin: '0 auto 30px auto',
    textAlign: 'center',
  },
  title: {
    fontSize: '28px',
    fontWeight: '700',
    color: '#1f2328',
    margin: '0 0 8px 0',
  },
  subtitle: {
    fontSize: '15px',
    color: '#636c76',
    margin: '0 0 24px 0',
  },
  nav: {
    display: 'inline-flex',
    backgroundColor: '#eaeef2',
    padding: '4px',
    borderRadius: '8px',
    gap: '4px',
  },
  tabButton: {
    padding: '8px 16px',
    border: 'none',
    borderRadius: '6px',
    backgroundColor: 'transparent',
    color: '#57606a',
    fontSize: '14px',
    fontWeight: '600',
    cursor: 'pointer',
    transition: 'all 0.2s ease',
  },
  activeTabButton: {
    backgroundColor: '#ffffff',
    color: '#0969da',
    boxShadow: '0 1px 3px rgba(0,0,0,0.1)',
  },
  main: {
    maxWidth: '800px',
    margin: '0 auto',
  },
  section: {
    marginBottom: '20px',
  },
};

export default App;