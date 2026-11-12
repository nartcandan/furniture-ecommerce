import type { AuthUser, Session } from '@/features/auth/authStore'
import { apiRequest } from './client'

interface AuthResponse {
  accessToken: string
  expiresAt: string
  user: AuthUser
}

export interface LoginInput {
  email: string
  password: string
}

export interface RegisterInput extends LoginInput {
  firstName: string
  lastName: string
}

function toSession(response: AuthResponse): Session {
  return { token: response.accessToken, expiresAt: response.expiresAt, user: response.user }
}

export async function login(input: LoginInput): Promise<Session> {
  return toSession(await apiRequest<AuthResponse>('/api/auth/login', { method: 'POST', body: input }))
}

export async function register(input: RegisterInput): Promise<Session> {
  return toSession(
    await apiRequest<AuthResponse>('/api/auth/register', { method: 'POST', body: input }),
  )
}
