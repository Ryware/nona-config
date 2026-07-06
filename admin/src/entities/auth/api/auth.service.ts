import { apiClient } from "../../../shared/api/client";
import { authStore } from "../model/store";
import type {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  RegisterResult,
  SsoConfig,
  InvitationDetails,
} from "../model/types";

export const authService = {
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>("/auth/login", credentials);
  },

  async register(data: RegisterRequest): Promise<RegisterResult> {
    return apiClient.post<RegisterResult>("/auth/register", data);
  },

  async requestPasswordReset(data: ForgotPasswordRequest): Promise<void> {
    await apiClient.post('/auth/forgot-password', data);
  },

  async resetPassword(_data: ResetPasswordRequest): Promise<void> {
    // TODO: Implement when backend endpoint is ready
    throw new Error(
      "Password reset functionality is not yet implemented on the backend"
    );
  },

  logout() {
    authStore.clearSession();
    window.location.href = "/login";
  },

  isAuthenticated(): boolean {
    return authStore.isAuthenticated();
  },

  getToken(): string | null {
    return authStore.getToken();
  },

  async firstTime(): Promise<boolean> {
    return apiClient.get("/auth/first-time");
  },

  async getSsoConfig(): Promise<SsoConfig> {
    return apiClient.get("/auth/sso/config");
  },

  async loginWithGoogle(idToken: string): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>("/auth/sso/google", { idToken });
  },

  async loginWithMicrosoft(idToken: string): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>("/auth/sso/microsoft", { idToken });
  },

  async getInvitation(token: string): Promise<InvitationDetails> {
    return apiClient.get(`/auth/invitations/${token}`);
  },

  async completeInvitationWithPassword(token: string, newPassword: string): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>(`/auth/invitations/${token}/password`, { newPassword });
  },

  async completeInvitationWithGoogle(token: string, idToken: string): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>(`/auth/invitations/${token}/sso/google`, { idToken });
  },

  async completeInvitationWithMicrosoft(token: string, idToken: string): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>(`/auth/invitations/${token}/sso/microsoft`, { idToken });
  },
};

