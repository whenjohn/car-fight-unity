const endpoint = process.argv[2];

const pages = await (await fetch(`${endpoint}/json/list`)).json();
const page = pages.find((candidate) => candidate.type === 'page');
if (!page?.webSocketDebuggerUrl) {
  throw new Error('No browser page is available for diagnostics.');
}

const socket = new WebSocket(page.webSocketDebuggerUrl);
socket.addEventListener('open', () => {
  socket.send(JSON.stringify({ id: 1, method: 'Runtime.enable' }));
  socket.send(JSON.stringify({ id: 2, method: 'Log.enable' }));
});
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Runtime.consoleAPICalled') {
    const values = message.params.args.map((value) => value.value ?? value.description ?? value.type);
    console.log(`console=${values.join(' ')}`);
  }
  if (message.method === 'Runtime.exceptionThrown' || message.method === 'Log.entryAdded') {
    console.log(`browser_error=${JSON.stringify(message.params)}`);
  }
});
