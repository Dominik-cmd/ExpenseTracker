export interface UserInfo {
  id: string;
  username: string;
  isAdmin: boolean;
  createdAt: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  isAdmin: boolean;
}
