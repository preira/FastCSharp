## Sequence Diagrams: Race Conditions in EventDrivenCircuitBreaker

A seguir estão diagramas de sequência que ilustram como as condições de corrida podem ocorrer devido à concorrência entre métodos públicos e a task assíncrona de recuperação (`TryToRecoverWithDelay`). Estes diagramas ajudam a visualizar como múltiplos pontos de entrada interagem com o estado interno e o `CancellationTokenSource`.

---

### 1. **Abertura do Circuito e Recuperação Assíncrona**

```mermaid
sequenceDiagram
    participant Thread1 as Thread 1
    participant Breaker as EventDrivenCircuitBreaker
    participant Task as TryToRecoverWithDelay (Task)

    Thread1->>Breaker: Open(duration)
    Breaker->>Breaker: Status = OPEN
    Breaker->>Breaker: OnOpenListenners?.Invoke()
    Breaker->>Task: TryToRecoverWithDelay() (async, não aguardado)
    Note right of Task: Task inicia, cria novo CancellationTokenSource

    alt Outro thread chama CancelBackoff antes do delay terminar
        participant Thread2 as Thread 2
        Thread2->>Breaker: CancelBackoff()
        Breaker->>Breaker: cancellationTokenSource.Cancel()
        Task->>Task: TaskCanceledException lançada
        Task->>Breaker: Closing()
        Breaker->>Breaker: OnResetListenners?.Invoke()
    end
```

---

### 2. **Concorrência entre CancelBackoff e TryToRecoverWithDelay**

```mermaid
sequenceDiagram
    participant Thread1 as Thread 1
    participant Breaker as EventDrivenCircuitBreaker
    participant Task as TryToRecoverWithDelay (Task)
    participant Thread2 as Thread 2

    Thread1->>Breaker: Open(duration)
    Breaker->>Task: TryToRecoverWithDelay() (async)
    Note right of Task: cancellationTokenSource = CTS1

    Thread2->>Breaker: CancelBackoff()
    Breaker->>Breaker: cancellationTokenSource.Cancel() (CTS1)
    Task->>Task: TaskCanceledException
    Task->>Breaker: Closing()
    Breaker->>Breaker: OnResetListenners?.Invoke()
    Breaker->>Breaker: cancellationTokenSource = null

    alt Thread1 chama Open novamente antes do Task terminar
        Thread1->>Breaker: Open(duration)
        Breaker->>Task: TryToRecoverWithDelay() (nova Task, CTS2)
        Note right of Task: CTS1 pode ser sobrescrito por CTS2
    end
```

---

### 3. **Concorrência entre múltiplos CancelBackoff**

```mermaid
sequenceDiagram
    participant Thread1 as Thread 1
    participant Breaker as EventDrivenCircuitBreaker
    participant Thread2 as Thread 2

    Thread1->>Breaker: CancelBackoff()
    Breaker->>Breaker: cancellationTokenSource.Cancel()
    Breaker->>Breaker: cancellationTokenSource = null

    Thread2->>Breaker: CancelBackoff()
    Breaker->>Breaker: cancellationTokenSource?.Cancel() (null, nada acontece)
```

---

### 4. **Concorrência entre TryToRecoverWithDelay e Closing**

```mermaid
sequenceDiagram
    participant Task as TryToRecoverWithDelay (Task)
    participant Breaker as EventDrivenCircuitBreaker
    participant Thread1 as Thread 1

    Task->>Breaker: await Task.Delay(...)
    alt Thread1 chama Closing durante o delay
        Thread1->>Breaker: Closing()
        Breaker->>Breaker: Status = HALF_CLOSED
        Breaker->>Breaker: OnResetListenners?.Invoke()
        Breaker->>Breaker: cancellationTokenSource?.Cancel()
    end
    Task->>Task: TaskCanceledException
    Task->>Breaker: Closing() (pode ser chamado novamente)
```

---

## **Resumo dos Riscos de Race Condition**

- **cancellationTokenSource** pode ser sobrescrito ou cancelado por múltiplos threads ou tasks concorrentes.
- **TryToRecoverWithDelay** pode ser iniciado múltiplas vezes sem aguardar a conclusão anterior, levando a múltiplos resets ou estados inconsistentes.
- **Métodos públicos** (`Open`, `CancelBackoff`, `Closing`) podem ser chamados em qualquer ordem e thread, concorrendo com a task assíncrona.

---

> **Sugestão:**  
> Para evitar estas condições de corrida, considere proteger o acesso ao `cancellationTokenSource` e ao estado do breaker com locks ou mecanismos de sincronização apropriados, e garantir que apenas uma task de recuperação possa estar ativa por vez.