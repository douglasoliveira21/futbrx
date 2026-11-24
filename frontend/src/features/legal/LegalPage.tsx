import { useQuery } from "@tanstack/react-query";
import { api } from "../../services/api";
import "./legal.css";

// Public pages (no sign-in): privacy policy and terms of service. They are the links the Google consent screen asks for.
const UPDATED = "24 de novembro de 2026";

type Section = { title: string; body: string[] };

const privacy = (contact: string): Section[] => [
  {
    title: "1. Quem somos",
    body: [
      "O futbrx é um jogo de gerenciamento de futebol (football manager) jogado no navegador. Esta política explica quais dados pessoais o jogo trata, para quê e quais são os seus direitos, de acordo com a Lei Geral de Proteção de Dados (LGPD, Lei 13.709/2018).",
      `O controlador dos dados é o administrador do futbrx.${contact ? ` Contato para assuntos de privacidade: ${contact}.` : " O contato para assuntos de privacidade é informado pelo administrador do serviço."}`,
    ],
  },
  {
    title: "2. Dados que tratamos",
    body: [
      "Dados da conta: nome de exibição, e-mail e, quando você cria a conta por senha, um resumo criptográfico da senha (a senha em si nunca é guardada).",
      "Entrar com Google: se você escolher essa opção, recebemos do Google apenas o seu nome e o e-mail verificado da conta. Não acessamos contatos, arquivos, agenda nem outros dados do Google, e não publicamos nada em seu nome.",
      "Dados do jogo: as carreiras que você cria, suas escolhas de clube, táticas, resultados e histórico, necessários para salvar o seu progresso.",
      "Dados de segurança: data e hora de acessos, o tipo de navegador e dispositivo (para listar e encerrar suas sessões) e registros de ações da conta, como login, troca de senha e exclusão.",
      "Cookies: usamos um cookie de sessão, restrito ao site e inacessível a scripts, para manter você conectado. Ele dura 14 dias se você marcar “Lembrar de mim” e termina ao fechar o navegador se não marcar. Não usamos cookies de publicidade nem de rastreamento.",
    ],
  },
  {
    title: "3. Para que usamos os dados",
    body: [
      "Criar e proteger a sua conta, manter você conectado e salvar as suas carreiras.",
      "Prevenir abuso e fraude, por exemplo limitando tentativas de acesso, e investigar problemas de segurança.",
      "Não vendemos dados pessoais, não os usamos para publicidade e não os compartilhamos com terceiros para fins de marketing.",
    ],
  },
  {
    title: "4. Base legal",
    body: ["Tratamos os dados para executar o serviço que você pediu ao criar a conta (execução de contrato), para cumprir obrigações legais e para a segurança do serviço (legítimo interesse)."],
  },
  {
    title: "5. Compartilhamento",
    body: [
      "Os dados ficam no servidor em que o jogo está hospedado e podem ser acessados apenas pelo administrador do serviço. Se você usa o login com Google, o Google trata os seus dados conforme a política de privacidade dele (policies.google.com/privacy).",
      "Podemos divulgar dados se houver ordem legal ou para proteger direitos e a segurança do serviço.",
    ],
  },
  {
    title: "6. Por quanto tempo guardamos",
    body: ["Os dados da conta e das carreiras ficam enquanto a conta existir. Ao excluir a conta, as carreiras e os dados pessoais são apagados, salvo o que a lei exigir guardar. Sessões expiradas são removidas automaticamente."],
  },
  {
    title: "7. Seus direitos",
    body: [
      "Você pode pedir confirmação de que tratamos seus dados, acesso, correção, exclusão da conta e dos dados, portabilidade e informações sobre compartilhamento, além de revogar consentimentos.",
      `Para exercer esses direitos, fale com o administrador${contact ? ` pelo e-mail ${contact}` : ""}. Você também pode encerrar suas sessões na área “Conta e sessões” do jogo.`,
    ],
  },
  {
    title: "8. Segurança",
    body: ["Usamos conexão criptografada (HTTPS), senhas protegidas por resumo criptográfico, sessões com renovação limitada, proteção contra tentativas repetidas de acesso e controle de acesso por conta. Nenhum sistema é totalmente imune a falhas; se houver incidente que afete você, avisaremos conforme a lei."],
  },
  {
    title: "9. Crianças e adolescentes",
    body: ["O futbrx não é voltado a menores de 13 anos e não coleta dados de crianças de propósito. Menores de 18 anos devem usar o jogo com a ciência e a autorização de um responsável."],
  },
  {
    title: "10. Mudanças nesta política",
    body: ["Podemos atualizar esta política. A data da última atualização aparece no topo da página, e mudanças relevantes serão avisadas no jogo."],
  },
];

const terms = (contact: string): Section[] => [
  {
    title: "1. Aceitação",
    body: ["Ao criar uma conta ou entrar no futbrx, você concorda com estes Termos de Serviço e com a Política de Privacidade. Se não concordar, não use o jogo."],
  },
  {
    title: "2. O que é o futbrx",
    body: [
      "O futbrx é um jogo de simulação de gerenciamento de futebol, gratuito, de entretenimento. Os resultados, atletas, valores e eventos são simulados.",
      "O jogo é independente e não tem vínculo, patrocínio ou aprovação da CBF, de federações, de clubes, de ligas, da CONMEBOL ou de qualquer competição citada. Nomes de clubes e competições são usados apenas para identificar os campeonatos simulados; escudos e marcas pertencem aos seus titulares, e os que aparecem no jogo são usados conforme os créditos e licenças informados no próprio projeto. Os atletas e muitos dados são fictícios ou estimados.",
    ],
  },
  {
    title: "3. Sua conta",
    body: [
      "Você é responsável por manter sua senha em segredo e por tudo que acontece na sua conta. Informe dados verdadeiros e use um e-mail que seja seu. Avise o administrador se suspeitar de uso indevido.",
      "Você pode entrar com e-mail e senha ou com a sua conta Google. Uma conta criada por e-mail e senha e outra criada pelo Google são contas separadas, mesmo com o mesmo e-mail.",
    ],
  },
  {
    title: "4. Uso permitido",
    body: [
      "Você concorda em não: tentar acessar contas, dados ou sistemas que não são seus; interferir no funcionamento do jogo (ataques, sobrecarga, automação abusiva); explorar falhas de segurança em vez de relatá-las; usar o serviço para fins ilegais; ou copiar, revender ou distribuir o jogo sem autorização.",
      "Podemos suspender ou excluir contas que violem estes termos, com aviso quando possível.",
    ],
  },
  {
    title: "5. Conteúdo e propriedade",
    body: ["O código, o design, os textos e os elementos originais do futbrx pertencem aos seus autores. Você recebe uma licença pessoal, limitada e revogável para jogar. As carreiras que você cria são suas para jogar, mas existem apenas dentro do serviço."],
  },
  {
    title: "6. Disponibilidade e mudanças",
    body: ["O jogo é oferecido como está, em desenvolvimento, e pode ficar indisponível, mudar ou ser encerrado. Dados de carreiras podem ser alterados ou reiniciados por atualizações do jogo; faremos o possível para preservá-los, sem garantia."],
  },
  {
    title: "7. Limitação de responsabilidade",
    body: ["Na medida permitida pela lei, o administrador não responde por perdas indiretas, por perda de progresso no jogo ou por indisponibilidade do serviço. Nada nestes termos limita direitos que a lei garante ao consumidor."],
  },
  {
    title: "8. Mudanças nos termos",
    body: ["Podemos atualizar estes termos. Continuar usando o jogo depois da mudança significa que você a aceita; mudanças relevantes serão avisadas no jogo."],
  },
  {
    title: "9. Lei aplicável e contato",
    body: [
      "Estes termos seguem as leis do Brasil.",
      contact ? `Dúvidas ou solicitações: ${contact}.` : "Dúvidas ou solicitações devem ser feitas ao administrador do serviço.",
    ],
  },
];

export default function LegalPage({ kind }: { kind: "privacidade" | "termos" }) {
  const config = useQuery({ queryKey: ["auth-config"], queryFn: () => api<{ contactEmail?: string | null }>("/auth/config"), staleTime: Infinity, retry: false });
  const contact = config.data?.contactEmail ?? "";
  const isPrivacy = kind === "privacidade";
  const sections = isPrivacy ? privacy(contact) : terms(contact);
  return (
    <main className="legal">
      <header>
        <a href="/" className="legal-home">
          ← futbrx
        </a>
        <nav>
          <a href="/privacidade" aria-current={isPrivacy ? "page" : undefined}>
            Política de Privacidade
          </a>
          <a href="/termos" aria-current={!isPrivacy ? "page" : undefined}>
            Termos de Serviço
          </a>
        </nav>
      </header>
      <article>
        <h1>{isPrivacy ? "Política de Privacidade" : "Termos de Serviço"}</h1>
        <p className="legal-updated">Última atualização: {UPDATED}</p>
        {sections.map((s) => (
          <section key={s.title}>
            <h2>{s.title}</h2>
            {s.body.map((p) => (
              <p key={p}>{p}</p>
            ))}
          </section>
        ))}
      </article>
    </main>
  );
}
