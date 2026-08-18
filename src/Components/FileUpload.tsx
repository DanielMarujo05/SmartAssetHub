import React, { useState } from 'react';
import { Upload, CheckCircle2, AlertCircle, Loader2 } from 'lucide-react';
import { getUploadUrl, uploadFileToS3 } from '../Services/Api';

interface FileUploadProps {
  onUploadSuccess?: () => void;
}

export const FileUpload: React.FC<FileUploadProps> = ({ onUploadSuccess }) => {
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [isError, setIsError] = useState<boolean>(false);

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    try {
      setIsUploading(true);
      setIsError(false);
      setStatusMessage(`Gerando URL de upload para ${file.name}...`);

      const { uploadUrl } = await getUploadUrl(file.name);

      setStatusMessage('Enviando arquivo para a AWS...');

      await uploadFileToS3(uploadUrl, file);

      setStatusMessage('Upload concluído! A IA está processando o documento...');
      
      if (onUploadSuccess) {
        onUploadSuccess();
      }
    } catch (error) {
      console.error('Erro no upload:', error);
      setIsError(true);
      setStatusMessage('Ocorreu um erro ao enviar o arquivo.');
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div style={styles.container}>
      <h3 style={styles.title}>Upload de Documentos / Imagens</h3>
      
      {/* Área de Seleção de Arquivos */}
      <label style={{ ...styles.dropzone, opacity: isUploading ? 0.6 : 1 }}>
        {isUploading ? (
          <Loader2 style={styles.spinnerIcon} />
        ) : (
          <Upload style={styles.icon} />
        )}
        
        <span style={styles.labelText}>
          {isUploading ? 'Processando...' : 'Clique ou arraste um arquivo (.pdf, .jpg, .png)'}
        </span>

        {/* Input escondido que escuta o clique do usuário */}
        <input
          type="file"
          accept="image/*,application/pdf"
          onChange={handleFileChange}
          disabled={isUploading}
          style={{ display: 'none' }}
        />
      </label>

      {/* Mensagem de Feedback Visual */}
      {statusMessage && (
        <div style={{
          ...styles.feedback,
          backgroundColor: isError ? '#ffebe9' : '#e6f4ea',
          color: isError ? '#cf222e' : '#1a7f37',
        }}>
          {isError ? <AlertCircle size={18} /> : <CheckCircle2 size={18} />}
          <span>{statusMessage}</span>
        </div>
      )}
    </div>
  );
};

// Estilos rápidos e inline (CSS em JS) para ficar com visual moderno sem complicação
const styles: { [key: string]: React.CSSProperties } = {
  container: {
    backgroundColor: '#ffffff',
    padding: '20px',
    borderRadius: '12px',
    border: '1px solid #e1e4e8',
    boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
  },
  title: {
    margin: '0 0 12px 0',
    fontSize: '16px',
    fontWeight: '600',
    color: '#24292e',
  },
  dropzone: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '24px',
    border: '2px dashed #0969da',
    borderRadius: '8px',
    backgroundColor: '#f6f8fa',
    cursor: 'pointer',
    transition: 'all 0.2s ease',
  },
  icon: {
    width: '32px',
    height: '32px',
    color: '#0969da',
    marginBottom: '8px',
  },
  spinnerIcon: {
    width: '32px',
    height: '32px',
    color: '#0969da',
    marginBottom: '8px',
    animation: 'spin 1s linear infinite',
  },
  labelText: {
    fontSize: '14px',
    color: '#57606a',
    fontWeight: '500',
  },
  feedback: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    marginTop: '12px',
    padding: '10px 14px',
    borderRadius: '6px',
    fontSize: '13px',
    fontWeight: '500',
  },
};