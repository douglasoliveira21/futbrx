# Visual e fontes reais — versões 0.8/0.9

Atualização 0.9: ver [regras, gestão e fontes](RULES-MARKET-AND-2026.md). A coleta ampliada tem 1.820 linhas candidatas dos vinte clubes. Cinco escudos foram incorporados, dezoito licenças foram revisadas e os demais arquivos têm pendências identificadas no manifesto. A base completa de atletas reais não foi publicada. O relato abaixo da primeira coleta/download permanece como histórico.

## O que mudou no replay

O jogador humano e a biblioteca de futebol foram concluídos na versão 0.8. O GLB local tem cerca de 1,27 MB, rosto, três cabelos, tons de pele, uniforme numerado, chuteiras e luvas. Seus 14 clipes incluem 11 coreografias originais de futebol e três clipes de locomoção derivados da Universal Animation Library de Quaternius (CC0). O manequim anterior foi substituído por geometria humana original estilizada; o chute genérico procedural foi substituído por clipes esqueléticos específicos nos perfis com GLB.

Consulte [arte humana e futebol](HUMAN-AND-FOOTBALL-ANIMATION.md) para autoria, licenças, sincronização, estúdio e verificações. O manifesto `frontend/public/data/asset-credits.json` identifica os componentes e o SHA-256 atual. O arquivo CC0 do autor cobre somente os componentes reutilizados, sem dedicar a arte original do projeto ao domínio público.

Cada atleta clona seu esqueleto e materiais. Geometria/clipes ficam em cache compartilhado. Materiais e texturas de ossos das instâncias são descartados ao fechar o replay. Máscaras de vértice permitem uniforme e pele diferentes com um material por mesh, reduzindo draw calls. O detalhe animado de dedos foi removido para reduzir o pacote. Em falha de GLB, o avatar geométrico continua disponível.

O estádio ganhou assentos instanciados, bancos, túnel, bandeirinhas, placas genéricas e telão com placar. Não são reproduções licenciadas de estádios reais. O áudio é sintetizado no projeto e usa quatro fontes HRTF ligadas à posição da câmera, sinais de gol/apito e controle de volume. Inicia apenas pelo botão Ambiente e suspende com pausa, fim de jogo e fora da visibilidade. Não inclui samples gravados de torcidas.

## Perfis e desempenho

- Leve: personagens geométricos, DPR 1, público/assentos reduzidos e sem sombras.
- Equilibrada: GLB, DPR 1,25 e sem sombras.
- Alta: GLB, DPR 1,6, mais público/assentos e sombras.
- Automática: começa pelo perfil conservador conforme memória/núcleos informados pelo navegador e pode reduzir para Leve se observar menos de 24 FPS. Seleções manuais são respeitadas.

O replay informa FPS, chamadas de desenho e triângulos medidos durante a execução. Essas leituras dependem de GPU, navegador, resolução e ambiente. Não há certificação de 30/60 FPS em aparelhos reais, nem otimização Draco/KTX2/CDN nesta versão. Novas partidas usam o motor v3. Os motores e snapshots v1/v2 foram preservados.

Recriar o GLB, com as dependências do frontend instaladas:

```powershell
python scripts/assets/fetch-player-source.py
node scripts/assets/build-footballer.mjs
```

A captura do asset original é verificada pelo hash registrado. Se a fonte mudar, o download para e exige nova revisão. O script de exportação usa somente Three.js já instalado; não executa código baixado do espelho. Ao regerar o asset, atualize conscientemente seu hash no manifesto depois de inspecionar o resultado.

## Dados reais: cobertura e limites

Temporada escolhida: 2026. Participantes conferidos em 21/11/2026 na página da CBF:
https://www.cbf.com.br/futebol-brasileiro/times/campeonato-brasileiro/serie-a/2026

A relação está em `data/real-2026/clubs.json`. Ela inclui Athletico-PR, Coritiba, Chapecoense e Remo, em vez dos participantes ilustrativos usados no catálogo fictício. Identificadores dos vinte clubes foram conferidos por rótulos/sitelinks Wikidata; os resultados estão em `wikidata-clubs.json` e `club-identity-evidence.json`.

A licença CC0 do Wikidata se refere aos dados estruturados:
https://www.wikidata.org/wiki/Wikidata:Licensing

Evidências parciais de atletas, nascimento, posição e vínculos estão em `roster-evidence.json`. São registros para revisão, não elencos atuais confirmados. Há vínculos antigos sem data de fim e posições genéricas; a ausência de término não comprova permanência em 2026. A primeira coleta em lote retornou 352 registros para parte dos clubes. Uma tentativa completa posterior excedeu o tempo de resposta. O provedor também retornou limitação de uma consulta por minuto durante indisponibilidade. Não contornamos esses limites nem publicamos dados incompletos.

```powershell
# Uma consulta em lote por execução; respeite Retry-After se o provedor limitar.
python scripts/data/collect-2026.py
# Recriar apenas o resumo local a partir do cache, sem rede:
python scripts/data/collect-2026.py --offline
```

A tela Dados e licenças mostra a situação de cada clube, sem substituir o catálogo fictício nem modificar saves. Ela não identifica qualquer conjunto parcial como base jogável real. O levantamento não altera regras, datas do calendário demonstrativo, habilidade, salários ou valores de mercado.

## Gerar um catálogo revisado

O arquivo `approved-players.json` começa vazio. Cada linha deve conter `entity` (URL HTTPS Wikidata/QID), `clubId`, `name`, `position` (GOL/ZAG/LAT/MEI/ATA), `birthDate` (AAAA-MM-DD), `membershipVerified: true`, `verifiedOn` (data de referência) e `membershipSource` (URL de fonte atual que confirma o vínculo). Posição e permanência precisam de conferência; não aprove registros apenas porque surgiram na consulta.

```powershell
python scripts/data/build-reviewed-catalog.py
```

O gerador recusa clubes/elencos incompletos, IDs repetidos, idades inválidas, vínculos não conferidos ou data diferente da referência. Exige vinte clubes, 18–40 atletas por clube, dois goleiros e dezesseis jogadores de linha. Não produz arquivo parcial e não publica nada. Quando houver cobertura completa, cria `catalog-reviewed.json` para validar/importar no editor administrativo existente. O manifesto por atleta acompanha o JSON de saída; preserve-o junto da versão publicada.

Nomes/nascimentos/vínculos verificados são separados de parâmetros de jogo: habilidade 65, valor R$ 1 milhão, salário R$ 30 mil e força 70 são estimativas iniciais editáveis, nunca dados financeiros ou avaliações oficiais. Escudos não são inseridos pelo gerador.

## Escudos

As páginas a seguir foram conferidas e indicam PD-textlogo, com ressalvas de marcas:

- https://commons.wikimedia.org/wiki/File:Palmeiras_logo.svg
- https://commons.wikimedia.org/wiki/File:Clube_de_Regatas_do_Flamengo_logo.svg

O download dos SVGs retornou HTTP 403, portanto nenhum escudo foi incorporado. Não houve substituição por arquivos de outro site sem licença verificada. Demais arquivos continuam pendentes de revisão individual. A classificação de copyright de um arquivo não equivale a uma autorização geral de uso de marca, patrocínio ou representação oficial. Na 0.9, cinco arquivos verificados foram incluídos e os demais seguem o manifesto de revisão. Não há alegação de endosso dos clubes.

## Verificação

Build TypeScript/Vite, teste estrutural do GLB (skinning, independência de esqueletos, clipes, tamanho, hash/licença), teste de perfis, validação dos vinte participantes e cinco testes do gerador de catálogos revisados. A integração visual/áudio deve ser conferida no navegador; testes locais não substituem medições em GPUs e aparelhos distintos.
