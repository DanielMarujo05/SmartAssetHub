import axios from 'axios';

//url da api a ser consumida (API Gateway -> Lambda -> Bedrock)
const API_BASE_URL = 'https://yqstwmp417.execute-api.us-east-1.amazonaws.com/prod/';

// Interfaces para tipagem dos dados retornados pela API
export interface Asset {
  documentId: string;
  fileName: string;
  fileType: string;
  processedAt: string;
  tags?: string[];
  extractedText?: string;
}

export interface ChatResponse {
  answer: string;
}

export interface UploadUrlResponse {
  uploadUrl: string;
  key: string;
}

//cria uma instância do axios com a URL base da API
const api = axios.create({
  baseURL: API_BASE_URL,
});

//retorna metadados dos documentos já processados (GET /assets)
export const getAssets = async (): Promise<Asset[]> => {
  const response = await api.get<Asset[]>('/assets');
  return response.data;
};

//pega a URL para upload do arquivo no S3 (GET /upload-url?fileName=...)
export const getUploadUrl = async (fileName: string): Promise<UploadUrlResponse> => {
  const response = await api.get<UploadUrlResponse>(`/upload-url?fileName=${encodeURIComponent(fileName)}`);
  return response.data;
};

//envia o arquivo para o S3, Internamente processa as lambdas no Rekog,Compreheend e Textract
export const uploadFileToS3 = async (uploadUrl: string, file: File): Promise<void> => {
  await axios.put(uploadUrl, file, {
    headers: {
      'Content-Type': file.type,
    },
  });
};

//envia a pergunta do usuário para o Chatbot com Bedrock
export const sendChatMessage = async (question: string): Promise<string> => {
  const response = await fetch(`${API_BASE_URL}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ Question: question }),
  });

  const data = await response.json();

  if (!response.ok) {
    // Retorna a mensagem de detalhe enviada pelo catch da Lambda
    throw new Error(data.details || data.error || 'Erro desconhecido na API');
  }

  return data.answer;
};