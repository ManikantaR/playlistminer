import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiDelete } from '@/lib/api-client';

interface OAuthStatus {
  connected: boolean;
}

interface ConnectResponse {
  url: string;
}

export function useOAuthStatus() {
  return useQuery({
    queryKey: ['oauthStatus'],
    queryFn: () => apiGet<OAuthStatus>('/api/oauth/status'),
    refetchInterval: 10_000,
  });
}

export function useConnect() {
  return useMutation({
    mutationFn: async () => {
      const data = await apiGet<ConnectResponse>('/api/oauth/connect');
      window.location.href = data.url;
    },
  });
}

export function useDisconnect() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => apiDelete('/api/oauth/disconnect'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['oauthStatus'] });
    },
  });
}
