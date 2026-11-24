# Colocar o futbrx no ar pelo EasyPanel

Este roteiro usa **um serviço Compose**, que reúne o site, a API e o banco PostgreSQL. Você não precisa instalar .NET ou Node no servidor. O próprio deploy constrói as imagens.

Repositório: https://github.com/douglasoliveira21/futbrx

## 1. Tenha acesso ao painel

Tenha um servidor com EasyPanel instalado e acesso ao painel pelo navegador. Se usar um domínio próprio, tenha acesso também ao site onde administra o DNS desse domínio. Este roteiro começa com o EasyPanel já instalado.

Não utilize um serviço de site estático ou um único serviço App para este roteiro: o jogo também precisa da API e do banco.

## 2. Crie o projeto e o serviço

1. Entre no EasyPanel.
2. Clique em **Create Project / Criar projeto** e dê o nome `futbrx`.
3. Dentro do projeto, clique em **New Service / Novo serviço**.
4. Selecione **Compose** e use o nome `jogo`.
5. Na área **Source / Origem**, selecione **Git**.
6. Preencha e salve:

| Campo | O que colocar |
|---|---|
| Repository URL | `https://github.com/douglasoliveira21/futbrx.git` |
| Branch | `main` |
| Build Path | `/` |
| Docker Compose File | `compose.easypanel.yml` |

Os nomes dos botões podem variar conforme a versão e o idioma do painel. Se o repositório estiver privado, o painel mostrará uma chave SSH: adicione essa chave nas **Settings → Deploy keys** do repositório GitHub, com acesso somente de leitura.

Não escolha `compose.production.yml`: ele é um complemento de outra instalação, com Caddy. Neste roteiro, o HTTPS pertence ao EasyPanel.

## 3. Gere duas senhas diferentes

No seu computador Windows, abra o **PowerShell**, cole as linhas abaixo e pressione Enter:

```powershell
$bytes = New-Object byte[] 32
$generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$generator.GetBytes($bytes)
([BitConverter]::ToString($bytes)).Replace('-', '')
$generator.Dispose()
```

Vai aparecer uma sequência de 64 letras e números. Guarde-a como **senha do banco**. Execute novamente e guarde o segundo resultado como **chave de autenticação**. Não use o mesmo resultado nos dois campos. Não coloque esses valores no GitHub nem os envie no chat.

## 4. Configure o ambiente

No serviço `jogo`, abra **Environment / Ambiente**. Preencha:

```dotenv
POSTGRES_PASSWORD=COLE_AQUI_O_PRIMEIRO_RESULTADO
JWT_KEY=COLE_AQUI_O_SEGUNDO_RESULTADO
ADMIN_USER_IDS=
```

Substitua integralmente os dois textos depois do sinal `=` pelos valores gerados. Não deixe os textos de exemplo. Deixe `ADMIN_USER_IDS` vazio inicialmente: ele não é necessário para jogar.

Ative **Create .env file / Criar arquivo .env** e salve. Essa opção permite que o Compose leia os valores. A API já será iniciada em **Production**, com migrações automáticas, pelo arquivo preparado no repositório.

Guarde uma cópia segura desses valores. Depois de criar o banco, mudar `POSTGRES_PASSWORD` apenas no ambiente não muda a senha do banco existente; isso exige uma alteração coordenada no PostgreSQL. Trocar `JWT_KEY` invalida a autenticação dos usuários.

## 5. Faça o primeiro deploy

1. Clique em **Deploy**.
2. Abra o andamento do deploy e acompanhe os logs.
3. Aguarde a construção do frontend e da API. Na primeira vez também serão baixadas as imagens base.
4. O banco inicia, a API cria/atualiza as tabelas e carrega os catálogos, e o Nginx começa a servir o jogo.

O arquivo contém uma checagem de saúde do site passando pela API. Se a checagem estiver sendo exibida, aguarde ficar saudável. O primeiro deploy pode demorar vários minutos; o tempo depende do servidor e da conexão.

Os serviços internos são `db`, `api` e `web`. Não adicione portas públicas para o banco ou a API. Não crie Caddy separado para esta instalação.

## 6. Ligue o endereço do jogo

Abra **Domains / Domínios** no serviço Compose e adicione o endereço desejado. Configure:

| Campo | Valor |
|---|---|
| Domínio público | Por exemplo, `jogar.seudominio.com.br` |
| Serviço interno | `web` |
| Porta de destino | `80` |
| Protocolo interno | `HTTP` |
| HTTPS público | Ativado, com o certificado gerenciado pelo EasyPanel |

HTTP interno e HTTPS público são configurações diferentes: a ligação do painel ao Nginx usa HTTP, enquanto o jogador acessa HTTPS.

Para um domínio próprio, no administrador de DNS crie um registro **A**, nome `jogar`, apontando para o **IP público do servidor**. Use o domínio que você realmente possui, substituindo o exemplo acima. Aguarde a propagação e a emissão do certificado. Se o painel oferecer um domínio temporário com HTTPS, você pode usá-lo para o teste inicial.

## 7. Teste se funcionou

1. Abra `https://SEU_ENDERECO/api/health`. A resposta esperada é `Healthy`.
2. Abra `https://SEU_ENDERECO` e confira o cadeado de HTTPS.
3. Crie uma conta nova, entre e crie uma carreira.
4. Inicie uma partida; confira campo, jogadores, placar e controles.
5. Saia da conta e entre novamente.
6. Atualize a página e confira se a carreira continua disponível.
7. Depois de salvar o progresso, reinicie o serviço pelo painel e confira novamente a carreira. Não destrua o serviço para fazer esse teste.

O banco começa vazio: as contas e carreiras locais do seu PC não são transportadas pelo Git. O PostgreSQL do servidor guarda as novas contas e carreiras no volume persistente `postgres-data`.

A rota `?simulationTest=v11` é uma ferramenta de desenvolvimento e não funciona nesta instalação Production. Teste a transmissão iniciando uma partida na própria carreira.

## 8. Atualizações futuras

1. Confira no GitHub a aba **Actions**. O workflow **EasyPanel containers** testa o build dos contêineres, PostgreSQL, Nginx, cadastro, autenticação e início de partida.
2. Faça backup do banco antes de atualizações importantes. O volume persistente não substitui backup externo; este Compose não cria backups automáticos.
3. Com a alteração já enviada à branch `main`, volte ao serviço `jogo` no EasyPanel e clique em **Deploy**.
4. Aguarde os serviços e repita o teste de saúde e de login.

Não é necessário recriar o projeto para atualizar. Não destrua o serviço, não apague o volume e não troque o nome do projeto/serviço sem planejar a migração do banco. A configuração atual é para uma única instância da API; múltiplas réplicas exigem outra estratégia para migrações e partidas ao vivo.

## Se aparecer algum erro

| Sintoma | O que conferir |
|---|---|
| Variável obrigatória ausente | Preencha `POSTGRES_PASSWORD` e `JWT_KEY`, ative a criação do `.env`, salve e faça Deploy novamente. |
| Chave JWT curta | Use o segundo resultado completo do gerador, com 64 caracteres. |
| Erro no COPY ou arquivos não encontrados | Build Path deve ser `/`, arquivo `compose.easypanel.yml`, branch `main` atualizada. |
| `502 Bad Gateway` | Confira os logs de `api` e `db`, aguarde as migrações e confirme domínio apontando para `web:80`. |
| `password authentication failed` | Confirme se a senha configurada ainda é a senha original do volume do banco. Não apague o volume para resolver. |
| Login perde sessão | Use sempre o endereço HTTPS e confira se o navegador aceita os cookies do jogo. |
| `429 Too Many Requests` | Aguarde um minuto antes de tentar novamente. O limite de autenticação atual pode ser compartilhado entre usuários atrás do proxy. |
| Build interrompido por falta de memória | Consulte os recursos e os logs do servidor e libere/aumente a memória disponível antes de repetir o deploy. |
| Tela sem 3D em um aparelho | Confira WebGL/aceleração gráfica no navegador. A renderização 3D ocorre no dispositivo do jogador. |

Se precisar de ajuda com um erro, copie o trecho final do log sem senhas, tokens ou connection strings.

## Referências

- EasyPanel: https://easypanel.io/docs/services/compose
- Builds e contexto Docker: https://easypanel.io/docs/builders

Os arquivos foram preparados para esta topologia. Um teste local sem Docker não comprova o funcionamento dos contêineres; consulte o resultado do workflow **EasyPanel containers** e faça os testes do passo 7 no seu servidor antes de abrir para usuários.

## Entrar e registrar com Google

O botão "Fazer login com o Google" só aparece quando o servidor tem um Client ID configurado. Sem ele, a tela mostra apenas
e-mail e senha.

1. No Google Cloud Console, crie um **ID do cliente OAuth** do tipo **Aplicativo da Web**.
2. Em **Origens JavaScript autorizadas**, informe o endereço público do jogo (por exemplo `https://jogo.seudominio.com`; para testes
   locais, `http://localhost:5173`). Não é preciso URI de redirecionamento: o botão usa o fluxo de token de identidade.
3. No serviço `jogo`, em **Environment / Ambiente**, acrescente `Google__ClientId` com o ID do cliente (termina em
   `.apps.googleusercontent.com`; não é um segredo).
4. A política de segurança do proxy (`infrastructure/proxy/nginx.conf`) já libera os domínios do Google Identity Services.

Como funciona: o navegador recebe um token de identidade do Google e a API o valida em `oauth2.googleapis.com/tokeninfo`
(destinatário igual ao Client ID, emissor, validade e e-mail verificado) antes de abrir a sessão normal. O primeiro acesso cria a
conta com o nome e o e-mail do Google, sem senha. Uma conta já criada por e-mail e senha **não** é vinculada automaticamente ao
Google (o e-mail nunca foi verificado no cadastro por senha): o jogador recebe "Já existe uma conta com este e-mail. Entre com a
senha.". "Lembrar de mim" vale também para o login com Google.

### Campos da tela de consentimento do Google

Em **Domínio do app** (Google Auth Platform → Branding), preencha com o endereço público do jogo (troque pelo seu domínio):

| Campo | Valor |
|---|---|
| Página inicial do aplicativo | `https://SEU-DOMINIO/` |
| Link da Política de Privacidade | `https://SEU-DOMINIO/privacidade` |
| Link dos Termos de Serviço | `https://SEU-DOMINIO/termos` |

As páginas são públicas (abrem sem login) e a tela de login tem links para as duas. Para mostrar um e-mail de contato nelas, crie
no serviço `jogo` a variável `Legal__ContactEmail`; sem ela, o texto manda falar com o administrador do serviço. Inclua também o
domínio em **Domínios autorizados** do Google. O texto das páginas é um modelo baseado na LGPD: revise antes de publicar.
