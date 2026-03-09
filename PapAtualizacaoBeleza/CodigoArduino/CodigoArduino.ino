#include <Stepper.h>

const int stepsPerRevolution = 2048; 
const int passos90Graus = 512;

Stepper motor(stepsPerRevolution, 8, 10, 9, 11);

void setup() {
  Serial.begin(9600);
  motor.setSpeed(10);
}

void loop() {
  if (Serial.available() > 0) {
    char comando = Serial.read();

    if (comando == 'A') {
      motor.step(passos90Graus); 
    }
    else if (comando == 'F') {
      motor.step(-passos90Graus); 
    }
    // Ping de identificação — o C# envia 'P' para descobrir a porta certa.
    // Responde com a string de identificação única do projeto.
    else if (comando == 'P') {
      Serial.println("VAULTFACE_OK");
    }
  }
}
