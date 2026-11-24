import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { Eye, EyeOff, Loader2, Lock } from "lucide-react";
import { api } from "../../services/api";
import "./login.css";

// Page opened from the e-mail link (/redefinir-senha?token=...): the player chooses a new password.
export default function ResetPassword() {
  const token = new URLSearchParams(location.search).get("token") ?? "";
  const [password, setPassword] = useState("");
  const [again, setAgain] = useState("");
  const [show, setShow] = useState(false);
  const [problem, setProblem] = useState("");
  const reset = useMutation({ mutationFn: () => api<{ message: string }>("/auth/reset", { token, password }) });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (reset.isPending) return;
    if (password.length < 6) return setProblem("A senha precisa ter pelo menos 6 caracteres.");
    if (password !== again) return setProblem("As duas senhas precisam ser iguais.");
    setProblem("");
    reset.mutate();
  };
  return (
    <main className="login-stage compact">
      <div className="login-shade" aria-hidden />
      <div className="login-flow">
        <section className="login-panel" aria-label="Criar nova senha" style={{ marginTop: 40 }}>
          {reset.isSuccess ? (
            <div className="login-body login-recover" style={{ margin: "8px 0 0" }}>
              <h2>Senha alterada</h2>
              <p role="status">{reset.data.message}</p>
              <a className="login-cta" href="/" style={{ textDecoration: "none" }}>
                <span>Ir para o login</span>
              </a>
            </div>
          ) : !token ? (
            <div className="login-body login-recover" style={{ margin: "8px 0 0" }}>
              <h2>Link inválido</h2>
              <p>Abra o link completo que enviamos por e-mail, ou peça um novo em “Esqueceu sua senha?”.</p>
              <a className="login-link" href="/">
                Voltar ao login
              </a>
            </div>
          ) : (
            <form className="login-body" style={{ margin: "8px 0 0" }} onSubmit={submit} noValidate>
              <h2 style={{ margin: 0, fontSize: 22 }}>Criar nova senha</h2>
              <label className="login-field">
                <Lock size={23} aria-hidden />
                <input type={show ? "text" : "password"} value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Nova senha" maxLength={128} autoComplete="new-password" aria-label="Nova senha" autoFocus />
                <button type="button" className="login-eye" onClick={() => setShow(!show)} aria-label={show ? "Ocultar senha" : "Mostrar senha"} aria-pressed={show}>
                  {show ? <EyeOff size={24} /> : <Eye size={24} />}
                </button>
              </label>
              <label className="login-field">
                <Lock size={23} aria-hidden />
                <input type={show ? "text" : "password"} value={again} onChange={(e) => setAgain(e.target.value)} placeholder="Repita a nova senha" maxLength={128} autoComplete="new-password" aria-label="Repita a nova senha" />
              </label>
              <small className="login-hint">Mínimo de 6 caracteres. Ao trocar, todos os aparelhos precisam entrar de novo.</small>
              {(problem || reset.error) && (
                <p className="login-error" role="alert">
                  {problem || reset.error?.message}
                </p>
              )}
              <button type="submit" className="login-cta" disabled={reset.isPending} aria-busy={reset.isPending}>
                {reset.isPending ? <Loader2 className="spin" size={24} aria-hidden /> : null}
                <span>{reset.isPending ? "Salvando…" : "Salvar nova senha"}</span>
              </button>
            </form>
          )}
        </section>
      </div>
    </main>
  );
}
