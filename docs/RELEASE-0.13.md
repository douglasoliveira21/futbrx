# Entrega 0.13 — contato, arbitragem e apresentação

## O que mudou

Novas partidas usam positional-v6. V1 a V5 são preservadas; a retomada incremental seleciona V5 ou V6 pelo snapshot da partida, incluindo depois de reiniciar. O cálculo continua limitado ao tempo assistido e mantém resultados futuros privados.

A disputa de posse depende de distância e habilidade, com intervalo entre tentativas. O portador pode proteger a bola; desarmes em pé e carrinhos produzem ações/timestamps do servidor. Quedas e desequilíbrios têm recuperação e redução de movimento. Disputas aéreas podem alterar quem recebe o cruzamento. O controle da bola acompanha a orientação do jogador. O goleiro mantém a bola à altura das mãos ao encaixar. Isso é contato cinemático, sem solver de corpos articulados.

O jogador recebeu dedos, costuras e acabamento de tecido. A biblioteca tem vinte clipes: três de locomoção CC0 de Quaternius e dezessete coreografias originais, incluindo seis novos movimentos de contato/recuperação. A locomoção acompanha a distância interpolada, em vez de uma passada fixa independentemente do deslocamento. Modelo gerado por script, com aproximadamente 1,9 MB; testes percorrem todos os clipes, conferindo tracks, deformações, chão, esqueletos independentes e hashes. Continua uma direção estilizada original, sem captura de movimento ou aparência de atletas reais.

A entrada usa caminhada organizada a partir do túnel, árbitro acompanhando e corte para a formação. A iluminação e o relevo do gramado foram ajustados. A torcida tem variação de roupas/corpos e setor visitante; atualização das matrizes ocorre em 12 Hz no perfil Leve ou 24 Hz nos demais, preservando o ritmo do renderer. Pausa/visibilidade e redução automática por FPS continuam disponíveis.

## Arbitragem implementada e limites

Há vantagem em faltas leves com posse e ataque promissor, com retorno ao local original se a posse é perdida no período de observação. Cartões que exigem parada continuam tratados pela via anterior; não foi implementado um protocolo completo de punição diferida.

O impedimento do receptor é sancionado quando ele participa. Em finalizações, um atacante em posição irregular pode interferir se estiver efetivamente no corredor visual entre bola e goleiro. Um gol gera checagem, pausa curta e confirmação ou anulação por essa interferência; apenas gol confirmado entra no placar. As revisões não inventam decisões aleatórias para depois revertê-las. Não há revisão completa de pênaltis, vermelhos ou identidade.

Contato aéreo próximo de jogador com braços estendidos em disputa pode produzir infração simplificada de mão antinatural, com tiro livre ou pênalti conforme o local. As decisões puras distinguem posição natural, ação deliberada, gol imediatamente após toque do marcador e goleiro na própria área. O modelo não reconstrói toda a anatomia de cada contato nem todas essas categorias geram incidentes autônomos na simulação.

Referências do subconjunto adotado: [IFAB 2025/26, Lei 11](https://www.theifab.com/laws/2025-26/offside/), [Lei 12](https://www.theifab.com/laws/2025-26/fouls-and-misconduct/) e [protocolo VAR](https://www.theifab.com/laws/2025-26/video-assistant-referee-var-protocol/). A edição das regras é fixa no motor, sem atualizar partidas antigas.

Arbitragem integral permanece pendente: interferência em todos os contextos, mão anatômica completa, sanções diferidas, revisão integral, abandono por menos de sete atletas, W.O., protocolo de concussão e situações excepcionais. Não se deve apresentar esta versão como aplicação integral das leis.

## Interface

Departamento médico em cartões de condição/indisponibilidade, líderes estatísticos, contratos com prazo/salário e ações, histórico de transferências, academia/candidatos e relatórios de treino em cartões. Ações, revisões e confirmações permanecem nas rotas existentes. Extrato financeiro em registros visuais. Classificações continuam tabulares, por serem parte da apresentação esportiva. Configurações de gestão continuam exigindo seleções e formulários.

## Carregamento

O gráfico financeiro foi separado em módulo carregado sob demanda. O JavaScript principal minificado caiu de aproximadamente 892 kB para 480 kB (cerca de 46%); o módulo de gráficos ainda é baixado quando a tela o utiliza. O renderer 3D permanece sob demanda e mantém um bundle grande. A inicialização React foi separada dos componentes, evitando repetir createRoot durante atualizações do módulo principal.

## Dados de 2026

Em 21/11/2026, a consulta CC0 retornou HTTP 429 com Retry-After de 1.000 segundos. O download de escudos também permaneceu limitado. Não houve repetição agressiva, fabricação de aprovação ou publicação parcial: seis escudos incluídos, doze arquivos revisados aguardando download e dois sem origem/licença aprovada. Elencos completos continuam por conferir e publicar. A base jogável atual mantém atletas fictícios. Os calendários próprios anuais já atendem ao escopo.

## Validação

- 280 verificações na suíte geral de simulação/gestão; 103 na API, incluindo persistência SQLite e retomada após reinício.
- 52 verificações adicionais de contato/arbitragem, doze seeds, ordenação de eventos, correspondência de cada checagem à decisão e preservação da V5.
- Testes específicos de transmissão e futebol repetidos após ajustes finais; smoke HTTP e cinco testes Python de catálogo aprovados.
- Quatro suítes JavaScript aprovadas, incluindo vinte clipes, áudio, bola, arquibancada, fontes e reprodução.
- Build .NET sem avisos/erros; TypeScript/Vite aprovado, mantendo aviso de bundles grandes.
- Navegador: cartões médicos, contratos filtrados e abertura de renovação; 390×844 sem transbordamento horizontal. Conferência do carrinho no Estúdio, perfis Leve/Alta e partida V6 completa no fluxo obrigatório até 90+06:00: Palmeiras 1–2 Fluminense, salvo na rodada 3. Os gols tiveram checagem e confirmação antes de entrar no placar.

Não houve teste em aparelhos móveis/GPUs físicos distintos nem execução de PostgreSQL local. As medidas de fluidez do computador de desenvolvimento não são garantia para outros dispositivos.
