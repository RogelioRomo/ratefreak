import { apiRequest } from "@/shared/api/request";
import type { NotificationPage } from "../interfaces/notifications";
export const getNotifications = (page: number, signal?: AbortSignal) => apiRequest<NotificationPage>(`notifications?page=${page}&pageSize=20`, { signal });
export const getUnreadCount = (signal?: AbortSignal) => apiRequest<{ unreadCount: number }>("notifications/unread-count", { signal });
export const setNotificationRead = (id: string, read: boolean, signal?: AbortSignal) => apiRequest<void>(`notifications/${id}/read`, { method: read ? "PUT" : "DELETE", body: {}, signal });
export const deleteNotification = (id: string, signal?: AbortSignal) => apiRequest<void>(`notifications/${id}`, { method: "DELETE", body: {}, signal });
export const clearNotifications = (signal?: AbortSignal) => apiRequest<void>("notifications", { method: "DELETE", body: {}, signal });
export const readAllNotifications = (signal?: AbortSignal) => apiRequest<void>("notifications/read-all", { method: "POST", body: {}, signal });
