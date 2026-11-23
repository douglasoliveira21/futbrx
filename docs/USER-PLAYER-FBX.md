# Jogador FBX fornecido pelo usuário

## Entrada e conversão

Arquivo indicado: `soccer_player_rigged.fbx`. Uma cópia intacta permanece em `.runtime/assets/user/`, sem alterar o arquivo de Downloads. Não há animações no FBX original. Ele contém um mesh com 8.901 triângulos, 41 ossos e texturas incorporadas.

`scripts/assets/import-user-footballer.mjs` converte para `frontend/public/assets/players/user-footballer.glb`, extrai a base de cor e o mapa normal originais, normaliza orientação/altura e adapta os vinte clipes de futebol existentes ao rig. Vinte e dois ossos recebem retargeting; conectores e ossos auxiliares mantêm a hierarquia original. A superfície deformada é verificada durante a geração para corrigir penetração no gramado por diferenças de proporção. Os movimentos são adaptados, sem alegação de captura de movimento presente no FBX.

O arquivo glTF usa duas imagens externas locais, portanto deve permanecer acompanhado de `user-player-basecolor.jpg` e `user-player-normal.png`. O material é convertido para PBR com rugosidade e intensidade do mapa normal ajustadas; os mapas Phong de reflexão/brilho não são usados. O GLB tem aproximadamente 2,33 MB; texturas, aproximadamente 1,84 MB no total, compartilhadas pelos jogadores.

## Cores e papéis

O renderer recolore as regiões azuis da roupa, preservando iluminação e detalhes da base de cor. Atributo por vértice delimita o calção para ajustar também regiões neutras da peça. Pele, rosto, cabelo e chuteiras mantêm a textura fornecida. A textura original não é sobrescrita.

Jogadores de linha compartilham as cores da sua equipe. Goleiros usam cores diferentes dos jogadores de linha e entre si, com luvas. O árbitro recebe outra cor. Quando os tons das equipes são próximos, é escolhida uma cor alternativa para o visitante. A paleta representa as cores disponíveis no catálogo do jogo; não é uma coleção de estampas oficiais.

A numeração aparece somente nas costas, ampliada e com contorno para contraste. No Estúdio há seleção do clube, cor personalizada, número, uniforme de goleiro e botões Ver costas/Ver frente. As opções de pele/cabelo do antigo modelo foram retiradas, pois não correspondem à aparência incorporada neste FBX. O mesh é usado nos perfis Leve, Equilibrada e Alta; em falha do carregamento, continua disponível o fallback geométrico.

## Proveniência

Modelo e texturas fornecidos pelo usuário para esta integração local. Autoria e licença originais não foram inferidas nem reclassificadas como CC0. Rig/mesh/texturas são mantidos como componentes distintos das animações: três movimentos de locomoção derivam do material CC0 de Quaternius já incorporado; os demais são coreografias autorais do projeto. Créditos em Dados e licenças, `USER-MODEL.txt` e manifesto de hashes `user-player.json`.

## Verificação

Build TypeScript/Vite e testes de apresentação, reprodução, estádio/fontes e do modelo importado. O teste dedicado confere os hashes, vinte clipes, quarenta e um ossos, clones independentes, atributos de uniforme, limites da deformação, contato com o piso e separação das cores. Nos quadros amostrados, a menor altura ficou em aproximadamente -0,009 m, dentro da tolerância de quatro centímetros do teste. Conferência no navegador do uniforme por clube, da troca para goleiro pausado e do uso em replay com as duas equipes.

O motor esportivo, os resultados e os saves não foram modificados nesta integração visual.
