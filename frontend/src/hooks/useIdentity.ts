import { useQuery } from "@tanstack/react-query";
import { api } from "../services/api";
export function useIdentity() {
  return useQuery({
    queryKey: ["identity"],
    queryFn: () =>
      api<{ id: string; name: string; isAdmin: boolean }>("/auth/me"),
    staleTime: 60_000,
  });
}
