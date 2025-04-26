const ws = new WebSocket(`ws://${location.host}`);

ws.onopen = () => {
    console.log("Rover conectado!");
    ws.send("rover"); // identificação inicial obrigatória
};

ws.onmessage = (event) => {
    console.log("Comando recebido pelo rover:", event.data);
};

ws.onerror = (error) => {
    console.error("Erro no WebSocket do rover:", error);
};
