# Categorias de base, observação e promoção — versão 0.6

Regras de um jogo demonstrativo com atletas fictícios. Não reproduzem contratos de formação, registros de federação ou regulamentos oficiais.

## Elenco da base

Novas carreiras e saves anteriores recebem seis jovens, com pelo menos um goleiro e representantes das cinco posições. São gerados com 16–19 anos e habilidade de 43–65; IDs próprios não colidem com profissionais, agentes livres, contratos ou perfis de desenvolvimento existentes. O upgrade é idempotente e não cobra entrada nem modifica partidas anteriores.

A base tem até dezoito vagas. Manutenção: R$ 6.000 por atleta por rodada, cobrada ao final da rodada e registrada no livro financeiro. Esse custo representa estrutura/formação; o salário exibido na tela só integra a folha profissional depois da promoção. Assim como a folha já existente, a manutenção continua sendo cobrada mesmo que torne o saldo negativo. Para contratar observação, captar ou promover, o caixa precisa comportar o desembolso.

Jovens da base participam do mesmo microciclo do clube e podem receber foco individual na tela Treinamentos. Os relatórios identificam Base ou Profissional e conservam essa informação no histórico. Atributos e XP acompanham o ID do atleta. Não há partidas de base, estatísticas competitivas sub-20, potencial individual ou simulação de campeonatos juvenis nesta etapa.

## Observação

Escolha uma das cinco regiões brasileiras e uma posição, ou Todas. O custo é de R$ 150.000, debitado ao confirmar. A taxa não é reembolsável; só há uma observação em andamento. A região identifica a origem fictícia do relatório e altera sua geração, sem bônus de qualidade por região.

Uma busca iniciada na rodada R é concluída no início de R+2, após avançar duas rodadas. O relatório encontra seis candidatos. Consulta da página não avança o prazo nem cobra novamente; candidatos e seed não são enviados antes da conclusão. As posições respeitam o filtro, e os jovens têm a mesma faixa inicial de idade/habilidade da formação local.

O relatório vale até a rodada de conclusão +4, limitada à 39, inclusive. Ao ultrapassar o prazo, os candidatos são removidos. Um novo pedido depois da conclusão descarta os candidatos restantes do relatório anterior; a confirmação informa esse efeito. Observações podem começar até a rodada 36. A virada do ano remove a missão e os candidatos do ano anterior.

## Captação

Cada candidato incorporado custa R$ 50.000. O servidor revalida relatório, validade, saldo e vaga antes de mover o jovem para a base. Não existe contrato profissional nessa operação. O novo integrante ganha perfil de desenvolvimento e pode treinar a partir da próxima rodada. Não pode ser captado duas vezes, e não é adicionado ao mercado profissional de outros clubes.

## Promoção

Qualquer jovem da base pode ser promovido se houver vaga no profissional (limite de quarenta) e saldo para R$ 100.000 de luvas. A confirmação mostra as luvas, salário por rodada e término do contrato. O salário é definido na geração do atleta: `(habilidade inicial - 35) × R$ 1.000`; evoluções de treino não o reajustam automaticamente.

A promoção remove o jogador da base e o adiciona ao profissional, preservando identidade, condição, atributos e XP. Cria contrato ativo até o fim da temporada atual +2, lança luvas no caixa e registra uma movimentação do tipo `academy`. O atleta não se torna titular automaticamente; ajuste o Elenco. A partir de sua participação no profissional, estatísticas de partida e suspensão seguem as regras normais. Promoção é permitida fora das janelas demonstrativas do mercado.

Não há taxa entre clubes ou receita criada pela promoção. O valor estimado do jogador não é um recebimento: vendas posteriores continuam exigindo comprador, caixa e validação de contrato pelo mercado existente.

## Virada do ano

Jovens da base envelhecem uma vez por temporada. Quem completar 21 anos sem promoção sai como agente livre, sem receita de transferência, preservando seu desenvolvimento. Os demais permanecem. Entram até três novos jovens por ano, limitados às vagas disponíveis, sem taxa de captação; a manutenção passa a valer nas rodadas seguintes. Seis é apenas o tamanho inicial, não uma meta de reposição automática.

Profissionais promovidos seguem o envelhecimento e os vencimentos de contrato do elenco profissional. A virada não duplica idade, ingresso ou perfil ao apenas carregar o save. O histórico da base guarda até cinquenta registros; relatórios de treino mantêm a retenção de doze semanas já existente.

## API e persistência

Todas as rotas exigem autenticação e dono da carreira. Escritas exigem a revisão atual da carreira e geram auditoria:

- `GET /api/careers/{id}/youth/`: elenco, missão, candidatos, custos, vagas, caixa e histórico.
- `POST /api/careers/{id}/youth/scouting`: `{ revision, region, position }`.
- `POST /api/careers/{id}/youth/prospects/{playerId}/recruit`: `{ revision }`.
- `POST /api/careers/{id}/youth/players/{playerId}/promote`: `{ revision }`.

Valores de taxa, salário e luvas enviados pelo navegador são ignorados. Validações antecedem débitos e mudanças de elenco. Revisões antigas retornam conflito; conta alheia recebe 404. O estado adicional permanece no JSON da carreira, sem migração relacional. A API de treino inclui jovens da base com `group: Base`, sem expor elencos de outros usuários.

Testes locais cobrem prazos, valores, repetição, limites, identidade, evolução, envelhecimento, saves, autorização e temporada completa. PostgreSQL, infraestrutura pública e partidas de base seguem pendentes.
