import { useEffect, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { ArrowRight, BarChart3, Eye, EyeOff, Gamepad2, Loader2, Lock, Network, Trophy, User } from "lucide-react";
import { api, setToken } from "../../services/api";
import { CompetitionEmblem, GoogleMark, type Emblem } from "./LoginArt";
import { FutbrxLogo } from "../../components/FutbrxLogo";
import { COMPACT_WIDTH, stageScale } from "./loginLayout";
import "./login.css";

declare const __APP_VERSION__: string;
const version = typeof __APP_VERSION__ === "string" ? __APP_VERSION__ : "";

const features = [
  { icon: Gamepad2, label: "Gerencie seu clube", accent: true },
  { icon: BarChart3, label: "Monte o elenco" },
  { icon: Network, label: "Defina táticas" },
  { icon: Trophy, label: "Conquiste títulos" },
];
const competitions: { emblem: Emblem; label: string }[] = [
  { emblem: "brasileirao", label: "BRASILEIRÃO SÉRIE A · B · C · D" },
  { emblem: "copa", label: "COPA DO BRASIL" },
  { emblem: "libertadores", label: "LIBERTADORES" },
  { emblem: "sulamericana", label: "SUL-AMERICANA" },
  { emblem: "estaduais", label: "ESTADUAIS" },
];

function useViewport() {
  const read = () => ({ width: window.innerWidth, height: window.innerHeight });
  const [size, setSize] = useState(read);
  useEffect(() => {
    const onResize = () => setSize(read());
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);
  return size;
}

type Mode = "login" | "register" | "recover";
type LoginResult = { token: string; user: { name: string } };

// Google Identity Services, loaded only when the server has a Google client ID.
const GOOGLE_SCRIPT = "https://accounts.google.com/gsi/client";
let googleScript: Promise<void> | null = null;
function loadGoogle(): Promise<void> {
  googleScript ??= new Promise((resolve, reject) => {
    const el = document.createElement("script");
    el.src = GOOGLE_SCRIPT;
    el.async = true;
    el.onload = () => resolve();
    el.onerror = () => {
      googleScript = null;
      reject(new Error("Não foi possível carregar o login do Google."));
    };
    document.head.appendChild(el);
  });
  return googleScript;
}

export default function LoginScreen({ onLogin }: { onLogin: (name: string) => void }) {
  const [mode, setMode] = useState<Mode>("login");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(true);
  const [show, setShow] = useState(false);
  const [problem, setProblem] = useState("");
  const submitting = useRef(false);
  const googleBox = useRef<HTMLDivElement>(null);
  const rememberNow = useRef(remember);
  rememberNow.current = remember;
  const view = useViewport();
  const compact = view.width < COMPACT_WIDTH;
  const scale = stageScale(view.width, view.height);
  const register = mode === "register";

  const m = useMutation({
    mutationFn: () =>
      api<{ token: string; user: { name: string } }>("/auth/" + (register ? "register" : "login"), {
        email: email.trim(),
        password,
        name: register ? name.trim() : "Treinador",
        remember,
      }),
    onSuccess: (r) => {
      setToken(r.token);
      onLogin(r.user.name);
    },
    onSettled: () => {
      submitting.current = false;
    },
  });

  const config = useQuery({ queryKey: ["auth-config"], queryFn: () => api<{ googleClientId: string | null }>("/auth/config"), staleTime: Infinity, retry: false });
  const googleId = config.data?.googleClientId ?? null;
  const google = useMutation({
    mutationFn: (credential: string) => api<LoginResult>("/auth/google", { credential, remember: rememberNow.current }),
    onSuccess: (r) => {
      setToken(r.token);
      onLogin(r.user.name);
    },
  });
  const googleSubmit = google.mutate;
  // Renders Google's own button (the only way to get a valid ID token) in the form of the current tab.
  useEffect(() => {
    if (!googleId || mode === "recover") return;
    let cancelled = false;
    loadGoogle()
      .then(() => {
        const api = (window as unknown as { google?: { accounts: { id: { initialize: (o: object) => void; renderButton: (el: HTMLElement, o: object) => void } } } }).google;
        if (cancelled || !api || !googleBox.current) return;
        api.accounts.id.initialize({ client_id: googleId, callback: (r: { credential: string }) => googleSubmit(r.credential), ux_mode: "popup" });
        api.accounts.id.renderButton(googleBox.current, { theme: "filled_black", size: "large", shape: "rectangular", width: 400, locale: "pt-BR", text: mode === "register" ? "signup_with" : "signin_with", logo_alignment: "left" });
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [googleId, mode, googleSubmit]);

  const validate = () => {
    if (register && !name.trim()) return "Informe o nome do treinador.";
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) return "Informe um e-mail válido.";
    if (!password) return "Informe a senha.";
    if (register && password.length < 12) return "A senha precisa ter pelo menos 12 caracteres.";
    return "";
  };
  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (submitting.current || m.isPending) return;
    const message = validate();
    setProblem(message);
    if (message) return;
    submitting.current = true;
    m.mutate();
  };
  const choose = (next: Mode) => {
    setMode(next);
    setProblem("");
    m.reset();
  };
  const error = problem || (m.error ? m.error.message : "") || (google.error ? google.error.message : "");

  const panel = (
    <section className={"login-panel" + (register ? " is-register" : "") + (mode === "recover" ? " is-recover" : "")} aria-label="Acesso ao jogo">
      <div className="login-tabs" role="tablist">
        <button type="button" role="tab" aria-selected={mode !== "register"} className={mode !== "register" ? "on" : ""} onClick={() => choose("login")}>
          Entrar
        </button>
        <button type="button" role="tab" aria-selected={register} className={register ? "on" : ""} onClick={() => choose("register")}>
          Criar Conta
        </button>
      </div>
      {mode === "recover" ? (
        <div className="login-body login-recover" key="recover">
          <h2>Recuperar acesso</h2>
          <p>
            Ainda não há envio de e-mail para redefinir a senha. Peça a redefinição ao administrador do jogo: ele cria uma
            nova senha e encerra as sessões antigas. Depois é só entrar de novo.
          </p>
          <button type="button" className="login-link" onClick={() => choose("login")}>
            Voltar ao login
          </button>
        </div>
      ) : (
        <form className="login-body" key={mode} onSubmit={submit} noValidate>
          {register && (
            <label className="login-field">
              <User size={24} aria-hidden />
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Nome do treinador" maxLength={60} autoComplete="name" aria-label="Nome do treinador" />
            </label>
          )}
          <label className="login-field">
            <User size={24} aria-hidden />
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="E-mail"
              maxLength={254}
              autoComplete="email"
              aria-label="E-mail"
            />
          </label>
          <label className="login-field">
            <Lock size={23} aria-hidden />
            <input
              type={show ? "text" : "password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Senha"
              maxLength={128}
              autoComplete={register ? "new-password" : "current-password"}
              aria-label="Senha"
            />
            <button type="button" className="login-eye" onClick={() => setShow(!show)} aria-label={show ? "Ocultar senha" : "Mostrar senha"} aria-pressed={show}>
              {show ? <EyeOff size={24} /> : <Eye size={24} />}
            </button>
          </label>
          {register && <small className="login-hint">Mínimo de 12 caracteres.</small>}
          <div className="login-row">
            <label className="login-check">
              <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
              <span aria-hidden className="box" />
              Lembrar de mim
            </label>
            {!register && (
              <button type="button" className="login-link" onClick={() => choose("recover")}>
                Esqueceu sua senha?
              </button>
            )}
          </div>
          {error && (
            <p className="login-error" role="alert">
              {error}
            </p>
          )}
          {googleId && (
            <div className="login-google">
              <div className={"login-google-card" + (google.isPending ? " busy" : "")}>
                <GoogleMark />
                <span>{google.isPending ? "Conectando…" : register ? "Cadastrar com o Google" : "Entrar com o Google"}</span>
                {/* Google's own button, invisible, stretched over the card: it provides the real ID token on click. */}
                <div className="login-google-hit" aria-label={register ? "Cadastrar com o Google" : "Entrar com o Google"}>
                  <div ref={googleBox} className="login-google-button" />
                </div>
              </div>
              <p>
                <i /> <span>ou use seu e-mail</span> <i />
              </p>
            </div>
          )}
          <button type="submit" className="login-cta" disabled={m.isPending || google.isPending} aria-busy={m.isPending || google.isPending}>
            {m.isPending ? <Loader2 className="spin" size={24} aria-hidden /> : null}
            <span>{m.isPending ? "Conectando…" : register ? "Criar conta e entrar" : "Entrar no Jogo"}</span>
            {!m.isPending && <ArrowRight size={24} aria-hidden />}
          </button>
        </form>
      )}
      {!register && mode !== "recover" && (
        <ul className="login-features" aria-label="O que você faz no jogo">
          {features.map(({ icon: Icon, label, accent }) => (
            <li key={label}>
              <Icon size={30} className={accent ? "accent" : ""} aria-hidden />
              <span>{label}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );

  const content = (
    <>
      <div className="login-logo">
        <FutbrxLogo />
      </div>
      <p className="login-subtitle">
        <i /> <span>FOOTBALL MANAGER</span> <i />
      </p>
      {panel}
      <p className="login-champs-title">
        <i /> <span>Campeonatos</span> <i />
      </p>
      <ul className="login-champs">
        {competitions.map((c) => (
          <li key={c.emblem}>
            <CompetitionEmblem name={c.emblem} />
            <span>{c.label}</span>
          </li>
        ))}
      </ul>
    </>
  );

  return (
    <main className={"login-stage" + (compact ? " compact" : "")}>
      <div className="login-shade" aria-hidden />
      {compact ? (
        <div className="login-flow">{content}</div>
      ) : (
        <div className={"login-canvas" + (googleId ? " has-google" : "")} style={{ transform: `translate(-50%, -50%) scale(${scale})` }}>
          {content}
        </div>
      )}
      {version && <small className="login-version">v{version}</small>}
      <nav className="login-legal" aria-label="Documentos legais">
        <a href="/privacidade">Política de Privacidade</a>
        <a href="/termos">Termos de Serviço</a>
      </nav>
    </main>
  );
}
