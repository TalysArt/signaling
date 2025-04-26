const status = document.getElementById('status');

// **SEM** “/ws” no path — o servidor aceita WS na raiz
const socket = new WebSocket(`ws://${location.host}`);

socket.onopen = () => {
  status.innerText = 'WebSocket conectado!';
};

socket.onmessage = e => {
  status.innerText = 'Recebido do servidor: ' + e.data;
};

socket.onerror = e => {
  console.error('Erro no WebSocket:', e);
  status.innerText = 'Erro no WebSocket (veja console)';
};

socket.onclose = () => {
  status.innerText = 'WebSocket fechado.';
};
