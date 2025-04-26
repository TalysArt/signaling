let name;
let connectedUser;
let protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
let socket = new WebSocket('ws://localhost:9090');
let pc;
let dc;


socket.onopen = () => {
    console.log('Conectado ao servidor de sinalização');
};

socket.onmessage = (msg) => {
    let data = JSON.parse(msg.data);
    switch (data.type) {
        case 'login':
            if (data.success) {
                document.getElementById('loginPage').style.display = 'none';
                document.getElementById('callPage').style.display = 'block';
            } else {
                alert('Nome de usuário já está em uso');
            }
            break;
        case 'offer':
            handleOffer(data.offer, data.name);
            break;
        case 'answer':
            handleAnswer(data.answer);
            break;
        case 'candidate':
            handleCandidate(data.candidate);
            break;
        case 'leave':
            handleLeave();
            break;
    }
};

socket.onerror = (err) => {
    console.log('Erro no WebSocket:', err);
};

function send(message) {
    if (socket.readyState === WebSocket.OPEN) {
        if (connectedUser) {
            message.name = connectedUser;
        }
        socket.send(JSON.stringify(message));
    } else {
        console.error('WebSocket não está aberto. Estado atual:', socket.readyState);
    }
}

// Login
document.getElementById('loginBtn').addEventListener('click', () => {
    name = document.getElementById('usernameInput').value;
    send({ type: 'login', name: name });
    if(name==console.error)(
        alert('falha ao conectar')
    )
});

// Iniciar Chamada
document.getElementById('callBtn').addEventListener('click', () => {
    let targetName = document.getElementById('callToUsernameInput').value;
    connectedUser = targetName;
    pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun2.1.google.com:19302' }]

    });
    dc = pc.createDataChannel('channel1', { reliable: true });
    dc.onmessage = (event) => {
        document.getElementById('chatarea').innerHTML += connectedUser + ': ' + event.data + '<br>';
    };
    pc.onicecandidate = (event) => {
        if (event.candidate) {
            send({ type: 'candidate', candidate: event.candidate });
        }
    };
    pc.createOffer().then(offer => {
        return pc.setLocalDescription(offer);
    }).then(() => {
        send({ type: 'offer', offer: pc.localDescription, name: targetName });
    });
});

// Manipular oferta recebida
function handleOffer(offer, name) {
    connectedUser = name;
    pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun2.1.google.com:19302' }]
    });
    pc.ondatachannel = (event) => {
        dc = event.channel;
        dc.onmessage = (event) => {
            document.getElementById('chatarea').innerHTML += connectedUser + ': ' + event.data + '<br>';
        };
    };
    pc.onicecandidate = (event) => {
        if (event.candidate) {
            send({ type: 'candidate', candidate: event.candidate });
        }
    };
    pc.setRemoteDescription(new RTCSessionDescription(offer)).then(() => {
        return pc.createAnswer();
    }).then(answer => {
        return pc.setLocalDescription(answer);
    }).then(() => {
        send({ type: 'answer', answer: pc.localDescription });
    });
}

// Manipular resposta recebida
function handleAnswer(answer) {
    pc.setRemoteDescription(new RTCSessionDescription(answer));
}

// Manipular candidato ICE recebido
function handleCandidate(candidate) {
    pc.addIceCandidate(new RTCIceCandidate(candidate));
}

// Manipular desconexão
function handleLeave() {
    connectedUser = null;
    pc.close();
    pc = null;
    dc = null;
    document.getElementById('chatarea').innerHTML += 'Conexão fechada<br>';
}

// Enviar mensagem
document.getElementById('sendMsgBtn').addEventListener('click', () => {
    let msg = document.getElementById('msgInput').value;
    if (dc && dc.readyState === 'open') {
        dc.send(msg);
        document.getElementById('chatarea').innerHTML += name + ': ' + msg + '<br>';
    } else {
        alert('Não conectado');
    }
});
socket.onopen = () => {
    console.log('Conectado ao servidor de sinalização');
    // Aqui você pode habilitar o botão de login ou enviar mensagens iniciais, se necessário
};

// Encerrar chamada
document.getElementById('hangUpBtn').addEventListener('click', () => {
    if (connectedUser) {
        send({ type: 'leave', name: connectedUser });
        handleLeave();
    }
});