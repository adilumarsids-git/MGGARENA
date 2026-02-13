import http from "http";

const port = process.env.PORT || 8080;

const rooms = [
  { room_id: "bronze-001", entry_fee: 100, max_players: 4, mode: "duel" },
  { room_id: "silver-001", entry_fee: 500, max_players: 8, mode: "battle" }
];

const server = http.createServer((req, res) => {
  if (req.url === "/health") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ status: "ok", service: "project-a-backend-stub" }));
    return;
  }

  if (req.url === "/rooms") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ rooms }));
    return;
  }

  res.writeHead(404, { "Content-Type": "application/json" });
  res.end(JSON.stringify({ error: "Not Found" }));
});

server.listen(port, () => {
  console.log(`[ProjectA Backend] Running on http://localhost:${port}`);
});
