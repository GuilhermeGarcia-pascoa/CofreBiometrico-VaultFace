<div align="center">

<img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"/>
<img src="https://img.shields.io/badge/Blazor-Server-5C2D91?style=for-the-badge&logo=blazor&logoColor=white"/>
<img src="https://img.shields.io/badge/Emgu_CV-4.12-00599C?style=for-the-badge&logo=opencv&logoColor=white"/>
<img src="https://img.shields.io/badge/Arduino-UNO-00979D?style=for-the-badge&logo=arduino&logoColor=white"/>
<img src="https://img.shields.io/badge/SQL_Server-LocalDB-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white"/>

<br/><br/>

# 🔐 VaultFace
### Cofre Biométrico com Reconhecimento Facial

**Prova de Aptidão Profissional · TGPSI · 2025–2026**  
*Guilherme Vommaro Garcia — Agrupamento de Escolas de Albergaria-a-Velha*

<br/>

> Substitui a chave do cofre pelo rosto do utilizador.  
> Reconhecimento facial em tempo real · Encriptação AES-256 · Controlo físico via Arduino.

</div>

---

## 📋 Índice

- [Sobre o Projeto](#sobre-o-projeto)
- [Funcionalidades](#funcionalidades)
- [Arquitetura](#arquitetura)
- [Tecnologias](#tecnologias)
- [Requisitos do Sistema](#requisitos-do-sistema)
- [Instalação e Configuração](#instalação-e-configuração)
- [Configuração do Email](#configuração-do-email)
- [Configuração do Arduino](#configuração-do-arduino)
- [Segurança](#segurança)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Evolução do Projeto](#evolução-do-projeto)

---

## Sobre o Projeto

O **VaultFace** é um sistema de cofre biométrico que permite o controlo de acesso exclusivamente através do reconhecimento facial do utilizador autorizado — sem palavras-passe, sem chaves físicas, sem cartões.

O sistema foi desenvolvido como Prova de Aptidão Profissional (PAP) do Curso Profissional de Técnico de Gestão e Programação de Sistemas Informáticos (TGPSI), integrando visão computacional em tempo real, criptografia de nível empresarial e controlo de hardware físico num único sistema coerente.

### Como funciona

```
Utilizador aproxima-se → Liveness detection → Reconhecimento LBPH → Score ponderado → Arduino abre a tranca
```

1. O utilizador posiciona o rosto perante a câmara e executa o gesto de liveness (aproximar/afastar a cabeça)
2. A câmara captura frames a 25 FPS — cada frame é analisado pelo algoritmo LBPH
3. O score acumula proporcionalmente à confiança do reconhecimento
4. Ao atingir o limiar (score ≥ 2.5 + liveness concluído), o Arduino recebe o comando de abertura
5. O utilizador é redirecionado para o seu painel pessoal

---

## Funcionalidades

### 🎯 Reconhecimento Facial
- Algoritmo **LBPH** (Local Binary Pattern Histogram) com limiar de confiança calibrado empiricamente
- **5 capturas biométricas** por utilizador com 32 variações automáticas cada (160 amostras de treino)
- Score ponderado proporcional à confiança — frames de alta confiança valem mais
- Debounce assimétrico: sobe rapidamente, desce gradualmente

### 🛡️ Segurança
- **Liveness detection** por tamanho do rosto — resiste a fotografias e vídeos
- **AES-256** com IV aleatório único por operação (envelope encryption)
- **DPAPI** do Windows para proteção da chave-mestra
- **PIN de emergência** com hash SHA-256 e lockout após 3 tentativas
- **Bloqueio automático** após 3 acessos falhados consecutivos (5 minutos)
- **Timeout de sessão** automático após 5 minutos de inatividade
- **Verificação por email** (OTP de 6 dígitos) para ações críticas de administração

### 🔧 Hardware
- Controlo do motor de passo via Arduino UNO
- Deteção automática da porta COM (sem configuração manual)
- **Sensor de porta** magnético (reed switch) com estado em tempo real na UI
- Watchdog no Arduino — fecha a tranca automaticamente se o heartbeat parar
- Reconexão automática quando o cabo USB é religado

### 📊 Administração
- Dashboard com 4 KPIs e gráfico SVG dos últimos 7 dias
- Gestão de utilizadores com 3 níveis de permissão
- Auditoria completa de logs com pesquisa e filtros
- Relatórios exportáveis em PDF (QuestPDF) com envio por email
- Área Master exclusiva do AdminSupremo

### 🎨 Interface
- ASP.NET Blazor Server com modo claro/escuro em todas as páginas
- Tipografia DM Sans, animações CSS, layout responsivo
- Foto de perfil real do utilizador (desencriptada em background)
- Re-treino de biometria sem perder a conta

---

## Arquitetura

```
┌──────────────────────────────────────────────────────────┐
│           CAMADA DE APRESENTAÇÃO (Blazor Server)          │
│  Home · Cadastro · Admin · UserLogado · Relatorios       │
└─────────────────────────┬────────────────────────────────┘
                          │ Injeção de Dependência
┌─────────────────────────▼────────────────────────────────┐
│              CAMADA DE LÓGICA (C# — Serviços)             │
│  BaseSql · EstadoApp · Emgu CV LBPH · AES-256 + DPAPI   │
│  EmailService · RelatorioPdfService · TemaService        │
└───────────┬──────────────────────────────┬───────────────┘
            │ SQL Queries                   │ SerialPort
┌───────────▼────────────┐   ┌─────────────▼──────────────┐
│     BASE DE DADOS      │   │    HARDWARE — Arduino UNO  │
│  BD Dinâmica (Rostos)  │   │  Motor de passo (tranca)   │
│  BD AppData (Chaves)   │   │  Sensor de porta (reed)    │
└────────────────────────┘   └────────────────────────────┘
```

### Encriptação em Dupla Camada (Envelope Encryption)

```
Imagem Facial
    │
    ├─ Gera IV aleatório (16 bytes)
    ├─ Gera chave aleatória AES-256 (32 bytes)
    ├─ Encripta: IV + AES(imagem, chave) → guarda em BD Dinâmica
    │
    └─ Encripta a chave: DPAPI(chave) → guarda em BD AppData
```

Sem acesso simultâneo às duas bases de dados **e** à chave-mestra DPAPI do perfil Windows, os dados biométricos são irrecuperáveis.

---

## Tecnologias

| Tecnologia | Versão | Função |
|---|---|---|
| C# / .NET | 10.0 | Linguagem principal |
| ASP.NET Blazor Server | .NET 10 | Interface web interativa |
| Emgu CV | 4.12.0 | Visão computacional (LBPH + HaarCascade) |
| SQL Server LocalDB | 2022 | Base de dados relacional |
| AES-256 + DPAPI | System.Security | Encriptação biométrica |
| QuestPDF | 2024.x | Geração de relatórios PDF |
| Arduino UNO | IDE 2.x | Controlo físico do cofre |
| DM Sans | Google Fonts | Tipografia da interface |

---

## Requisitos do Sistema

> ⚠️ **O VaultFace requer Windows 10/11.** A encriptação DPAPI e o SQL Server LocalDB são exclusivos do Windows. O sistema verifica o SO no arranque e termina com uma mensagem clara se não for Windows.

### Software obrigatório

- **Windows 10/11** (64-bit)
- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10)
- **SQL Server LocalDB 2022** — incluído no Visual Studio ou [download separado](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb)
- **Visual Studio 2022** (recomendado) ou VS Code com extensão C#

### Hardware opcional (para controlo físico)

- Arduino UNO com motor de passo 28BYJ-48 + driver ULN2003
- Reed switch magnético (para sensor de porta)
- O sistema funciona sem Arduino — o cofre virtual é operado pela UI

### Câmera

- Qualquer câmera USB ou integrada compatível com DirectShow
- Resolução mínima recomendada: 640×480 @ 25 FPS

---

## Instalação e Configuração

### 1. Clonar o repositório

```bash
git clone https://github.com/[teu-utilizador]/CofreBiometrico-VaultFace.git
cd CofreBiometrico-VaultFace/PapAtualizacaoBeleza
```

### 2. Restaurar dependências

```bash
dotnet restore
```

### 3. Configurar o email SMTP

Ver secção [Configuração do Email](#configuração-do-email) abaixo.

### 4. Compilar e executar

```bash
dotnet run
```

O sistema cria automaticamente as bases de dados em:
```
%APPDATA%\VaultFace\
```

Não é necessária nenhuma configuração manual de base de dados.

### 5. Primeiro acesso

1. Abre o browser em `https://localhost:7134`
2. O sistema redireciona automaticamente para o cadastro do primeiro utilizador
3. O primeiro utilizador criado torna-se automaticamente **AdminSupremo**
4. Introduz o nome, o email, e o código de verificação recebido por email
5. Realiza as 5 capturas biométricas com variação de ângulo entre cada uma
6. Guarda o PIN de emergência gerado — é a chave de último recurso

---

## Configuração do Email

O VaultFace usa SMTP para envio de códigos de verificação OTP, notificações de acesso e relatórios em PDF.

> 🔒 **Segurança:** As credenciais SMTP **nunca devem** ser colocadas no repositório Git. O ficheiro `appsettings.json` no repositório contém campos vazios propositadamente. As credenciais reais devem ser configuradas localmente.

### Opção A — Configuração direta (desenvolvimento local)

Editar `appsettings.json` localmente (este ficheiro não é enviado para o Git):

```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": "587",
  "SmtpUser": "o-teu-email@gmail.com",
  "SmtpPass": "a-tua-senha-de-app",
  "Remetente": "o-teu-email@gmail.com",
  "NomeRemetente": "VaultFace"
}
```

### Opção B — Variáveis de ambiente (recomendado)

```powershell
$env:Email__SmtpUser = "o-teu-email@gmail.com"
$env:Email__SmtpPass = "a-tua-senha-de-app"
```

### Como obter a Senha de App do Gmail

1. Acede à tua conta Google → **Segurança**
2. Ativa a **Verificação em dois passos** (obrigatório)
3. Em **Senhas de app**, cria uma nova app com o nome "VaultFace"
4. Copia a senha de 16 caracteres gerada — usa-a como `SmtpPass`

> Se o email não estiver configurado, o sistema arranca na mesma mas o cadastro do primeiro utilizador não enviará código de verificação. O sistema pode ser usado sem email para os utilizadores seguintes (que não requerem verificação).

---

## Configuração do Arduino

### Componentes necessários

| Componente | Função |
|---|---|
| Arduino UNO | Microcontrolador principal |
| Motor de passo 28BYJ-48 | Mecanismo de abertura/fecho |
| Driver ULN2003 | Controlador do motor |
| Reed switch magnético | Sensor de estado da porta |
| Fios de ligação | — |

### Ligações

```
Arduino → Motor de passo (via ULN2003):
  Pino 8  → IN1
  Pino 9  → IN2
  Pino 10 → IN3
  Pino 11 → IN4

Arduino → Reed switch:
  Pino 2  → Terminal do reed switch
  GND     → Outro terminal do reed switch
  (INPUT_PULLUP interno — sem resistência externa)
```

### Upload do firmware

1. Abre o Arduino IDE
2. Abre o ficheiro `CodigoArduino/CodigoArduino.ino`
3. Seleciona **Arduino UNO** como placa
4. Faz upload para o Arduino

### Deteção automática da porta COM

O VaultFace deteta automaticamente a porta COM do Arduino ao arrancar. Não é necessária nenhuma configuração manual. O sistema envia o comando `P` a cada porta disponível e aguarda a resposta `VAULTFACE_OK`.

Se o Arduino não for detetado, a interface mostra o estado "Arduino não encontrado" e o botão de abrir/fechar cofre fica inoperacional — o resto do sistema (reconhecimento facial, painel de admin, logs) continua a funcionar normalmente.

---

## Segurança

### Modelo de ameaças e mitigações

| Ameaça | Mitigação |
|---|---|
| Acesso com fotografia impressa | Liveness detection por tamanho do rosto |
| Força bruta ao PIN | Lockout após 3 tentativas, bloqueio 5 min |
| Acesso a ficheiros MDF | AES-256 com IV aleatório + DPAPI — dados irrecuperáveis sem perfil Windows |
| Sessão abandonada | Timeout automático de 5 minutos de inatividade |
| Ação crítica não autorizada | OTP de 6 dígitos por email com expiração de 10 min |
| Cofre aberto sem supervisão | Watchdog Arduino — fecha automaticamente se heartbeat parar |
| Credenciais SMTP expostas | Campos vazios no appsettings.json do repositório |

### Níveis de permissão

| Nível | Capacidades |
|---|---|
| **Básico** | Abrir/fechar cofre, painel pessoal, histórico, re-treino de biometria |
| **AdminComum** | Básico + gestão de utilizadores, logs, dashboard |
| **AdminSupremo** | Total + Área Master, relatórios PDF, reset, transferência de administração |

### Dados biométricos e RGPD

Os dados biométricos são dados de **categoria especial** ao abrigo do Artigo 9.º do RGPD. O VaultFace implementa:
- **Consentimento explícito** no registo (checkbox obrigatório antes de capturar biometria)
- **Privacy by design** — encriptação AES-256 antes de qualquer persistência
- **Armazenamento local** — os dados nunca saem da máquina, sem cloud, sem terceiros
- **Direito ao esquecimento** — exclusão completa via painel de admin remove utilizador e todas as biometrias

---

## Estrutura do Projeto

```
PapAtualizacaoBeleza/
├── Components/
│   ├── Layout/
│   │   └── EmptyLayout.razor          # Layout sem navbar para as páginas principais
│   └── Pages/
│       ├── Home.razor                  # Reconhecimento facial + liveness detection
│       ├── Cadastro.razor              # Wizard de registo biométrico (5 capturas)
│       ├── Admin.razor                 # Painel de administração (dashboard, users, logs)
│       ├── UserLogado.razor            # Painel do utilizador autenticado
│       ├── Relatorios.razor            # Relatórios PDF + email
│       └── VerificacaoSeguranca.razor  # Wizard OTP para ações críticas
├── CodigoArduino/
│   └── CodigoArduino.ino              # Firmware: motor + sensor + heartbeat + watchdog
├── BaseSql.cs                          # Acesso a BD + encriptação AES-256
├── ControladorHardware.cs              # Comunicação série com Arduino
├── EstadoApp.cs                        # Estado global da aplicação (Singleton)
├── EmailService.cs                     # SMTP — OTP, notificações, relatórios
├── RelatorioPdfService.cs              # Geração de PDF com QuestPDF
├── TemaService.cs                      # Modo claro/escuro (Singleton)
├── Program.cs                          # Startup + verificação de SO + endpoints
├── appsettings.json                    # Configurações (sem credenciais)
└── haarcascade_frontalface_default.xml # Classificador Haar para deteção facial
```

---

## Evolução do Projeto

| Versão | Plataforma | Características |
|---|---|---|
| **v1.0** | .NET 4.2 + Windows Forms | Reconhecimento básico single-user |
| **v2.0 Beta** | .NET 8 + Windows Forms + LocalDB | Multi-user, AES, permissões |
| **v3.0 Final** | .NET 10 + Blazor Server | Interface web, liveness, sensor de porta, dashboard, relatórios PDF, Área Master |

---

<div align="center">

**VaultFace** · PAP 2025–2026  
Guilherme Vommaro Garcia · TGPSI 3.ºF  
Agrupamento de Escolas de Albergaria-a-Velha

</div>
