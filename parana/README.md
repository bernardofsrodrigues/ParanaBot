# Parana Bot

Este projeto é uma aplicação console escrita em C# que automatiza o processamento de jobs relacionados ao FGTS. Ele se conecta a um banco de dados MySQL para consultar e atualizar informações, além de interagir com uma API externa para obter e simular dados de saldo do FGTS.

## Funcionalidades Principais

- **Processamento Contínuo de Jobs:** O programa executa um loop contínuo que verifica e processa jobs pendentes na tabela `jobs_fgts` do banco de dados.
- **Consulta à API Externa:** Utiliza `HttpClient` para enviar solicitações HTTP a uma API externa do banco Paraná, obtendo dados de saldo e simulando saques de FGTS.
- **Atualização do Banco de Dados:** Atualiza as informações no banco de dados MySQL com os resultados das consultas e simulações.

## Tecnologias Utilizadas

- **C# e .NET:** Linguagem e framework principal usados para desenvolver a aplicação.
- **MySQL:** Banco de dados relacional usado para armazenar informações dos jobs e resultados das consultas.
- **HttpClient e Newtonsoft.Json:** Para fazer requisições HTTP à API externa e manipular dados JSON.

## Estrutura do Código

- **Program.cs:** Contém a lógica principal do programa, incluindo métodos para processar jobs (`ProcessJobsAsync`), enviar consultas (`EnviarUnicoAsync`), garantir a validade do token de autenticação (`EnsureValidTokenAsync`), e realizar consultas de saldo e simulação (`FetchSaldoAsync` e `FetchSimulacaoAsync`).

## Fluxo de Execução

1. **Início do Loop:** O método `Main` inicia um loop contínuo para processar jobs.
2. **Processamento de Jobs:** `ProcessJobsAsync` verifica se há jobs pendentes e, se houver, chama `EnviarUnicoAsync` para processar cada job individualmente.
3. **Consulta e Simulação:** `EnviarUnicoAsync` faz consultas de saldo e simulações de saque, atualizando os resultados no banco de dados.
4. **Atualização do Banco:** Os métodos `UpdateSaldoAsync` e `UpdateSimulacaoAsync` atualizam os dados no banco de dados.

## Configuração e Execução

### Configuração de Dados Sensíveis

Os dados sensíveis, como credenciais do banco de dados e da API, são ocultados e devem ser configurados usando variáveis de ambiente ou arquivos de configuração seguros.

### Execução

A aplicação pode ser executada com o comando `dotnet run` após configurar as variáveis de ambiente.

## Projeto Ideal

Este projeto é ideal para automatizar tarefas relacionadas ao processamento de FGTS, garantindo integração contínua entre um banco de dados MySQL e uma API externa. Ele é projetado para funcionar de forma ininterrupta, processando jobs de maneira eficiente e atualizando informações em tempo real.

## Segurança

Para garantir a segurança dos dados sensíveis:

- **Conexões com Banco de Dados:** Utilize variáveis de ambiente para armazenar strings de conexão e credenciais.
- **Autenticação de API:** Mantenha os tokens de API seguros e renováveis, garantindo que nunca expirem durante a operação.

## Desenvolvedor

Bernardo Ferreira Santos Rodrigues
