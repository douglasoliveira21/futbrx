import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Monitor, ShieldCheck } from "lucide-react";
import { api } from "../../services/api";
import { useIdentity } from "../../hooks/useIdentity";
import { Button } from "../../components/ui/button";
type Session = {
  id: string;
  created: string;
  expires: string;
  device: string;
  current: boolean;
};
export default function AccountSessions({
  onLogout,
}: {
  onLogout: () => void;
}) {
  const identity = useIdentity();
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["sessions"],
    queryFn: () => api<Session[]>("/auth/sessions/"),
  });
  const [confirm, setConfirm] = useState<Session | null>(null);
  const m = useMutation({
    mutationFn: (s: Session) => api("/auth/sessions/" + s.id + "/revoke", {}),
    onSuccess: (_, s) => {
      setConfirm(null);
      if (s.current) onLogout();
      else qc.invalidateQueries({ queryKey: ["sessions"] });
    },
  });
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">SUA CONTA</span>
          <h1>Sessões e acesso</h1>
          <p>Controle os dispositivos conectados à sua carreira.</p>
        </div>
        <ShieldCheck className="green" size={30} />
      </div>
      <section className="panel">
        <div className="panel-heading">
          <h2>{identity.data?.name}</h2>
          <span className="tag">
            {identity.data?.isAdmin ? "ADMINISTRADOR" : "TREINADOR"}
          </span>
        </div>
        <div className="form-stack">
          <label>
            ID da conta<code className="account-id">{identity.data?.id}</code>
          </label>
          <p>
            Sessões revogadas perdem acesso imediatamente, incluindo os tokens
            já emitidos. Cookies de sessão expiram em até 14 dias.
          </p>
        </div>
        {q.isPending && <p className="notice">Carregando sessões…</p>}
        {q.data?.map((s) => (
          <div className="session-row" key={s.id}>
            <Monitor size={23} />
            <div>
              <b>{s.current ? "Este dispositivo" : "Outra sessão"}</b>
              <small>{s.device || "Navegador sem identificação"}</small>
              <span>
                Criada em{" "}
                {new Date(s.created).toLocaleString("pt-BR", {
                  timeZone: "America/Sao_Paulo",
                })}{" "}
                · Expira em{" "}
                {new Date(s.expires).toLocaleDateString("pt-BR", {
                  timeZone: "America/Sao_Paulo",
                })}
              </span>
            </div>
            <Button
              variant="outline"
              disabled={m.isPending}
              onClick={() => setConfirm(s)}
            >
              Encerrar
            </Button>
          </div>
        ))}
        {(q.error || m.error) && (
          <p className="error notice" role="alert">
            {(q.error || m.error)?.message}
          </p>
        )}
      </section>
      {confirm && (
        <div className="modal-backdrop">
          <section
            className="panel modal"
            role="dialog"
            aria-modal="true"
            aria-label="Encerrar sessão"
          >
            <h2>
              Encerrar{" "}
              {confirm.current ? "esta sessão" : "a sessão selecionada"}?
            </h2>
            <p>
              {confirm.current
                ? "Você precisará entrar novamente."
                : "O dispositivo perderá o acesso e precisará entrar novamente."}
            </p>
            <div className="actions">
              <Button disabled={m.isPending} onClick={() => m.mutate(confirm)}>
                Encerrar sessão
              </Button>
              <Button variant="outline" onClick={() => setConfirm(null)}>
                Cancelar
              </Button>
            </div>
          </section>
        </div>
      )}
    </>
  );
}
