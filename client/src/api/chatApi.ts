import * as apiClient from './apiClient';
import type { ChatMessage, ChatSendResult } from '../types/game';

export function sendMessage(sessionId: string, message: string, token: string) {
  return apiClient.post<ChatSendResult>(`/api/chat/${sessionId}`, { message }, token);
}

export function getChatHistory(sessionId: string, token: string) {
  return apiClient.get<ChatMessage[]>(`/api/chat/${sessionId}`, token);
}
