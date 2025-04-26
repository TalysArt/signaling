const ws = new WebSocket(`ws://${location.host}`);

ws.onopen = () => {
    console.log("Conectado ao servidor WebSocket!");
    ws.send("controle"); // identificação inicial obrigatória
};

// Recebe mensagens do servidor
ws.onmessage = (event) => {
    console.log("Recebido do servidor:", event.data);
};

// Tratamento de erro
ws.onerror = (error) => {
    console.error("Erro no WebSocket:", error);
};

// Função para enviar comandos manualmente
function enviarComando(comando) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(comando);
        console.log("Comando enviado:", comando);
    } else {
        console.error("WebSocket não está aberto.");
    }
}
