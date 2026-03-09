#include <Stepper.h>

const int stepsPerRevolution = 2048;
const int passos90Graus = 512;

Stepper motor(stepsPerRevolution, 8, 10, 9, 11);

// ── Watchdog ─────────────────────────────────────────────────────────────────
// Se o programa C# parar (crash, fecho forçado, corte de energia no PC)
// sem fechar o cofre, o Arduino fecha-o sozinho ao fim de 5 segundos.
const unsigned long TIMEOUT_HEARTBEAT_MS = 5000;
bool cofreAberto = false;
unsigned long ultimoHeartbeat = 0;

void setup() {
  Serial.begin(9600);
  motor.setSpeed(10);
}

void loop() {

  // ── Verificação do watchdog ─────────────────────────────────────────────
  // Só ativa se o cofre estiver aberto E o C# não enviar heartbeat a tempo
  if (cofreAberto && (millis() - ultimoHeartbeat > TIMEOUT_HEARTBEAT_MS)) {
    motor.step(-passos90Graus); // fecha o cofre
    cofreAberto = false;
  }

  // ── Comandos recebidos via série ────────────────────────────────────────
  if (Serial.available() > 0) {
    char comando = Serial.read();

    if (comando == 'A') {
      motor.step(passos90Graus);
      cofreAberto = true;
      ultimoHeartbeat = millis(); // inicia o contador do watchdog
    }
    else if (comando == 'F') {
      motor.step(-passos90Graus);
      cofreAberto = false;
    }
    // Heartbeat — o C# envia 'H' a cada 2s enquanto o cofre está aberto.
    // Reseta o contador do watchdog sem mover o motor.
    else if (comando == 'H') {
      ultimoHeartbeat = millis();
    }
    // Ping de identificação — responde para o C# encontrar a porta certa.
    else if (comando == 'P') {
      Serial.println("VAULTFACE_OK");
    }
  }
}
