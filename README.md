![BitTorrent Blazor Client](https://raw.githubusercontent.com/JoshuaThadi/Wall-E-Desk/refs/heads/main/Pixel-Art/hsh1.gif)

# 🎯 Blazor BitTorrent Client

A minimal BitTorrent client built with **Blazor** and **C#**, inspired by the [Codecrafters BitTorrent Challenge](https://codecrafters.io). This project helped me explore peer-to-peer protocols, TCP handshakes, SHA-1 validation, and concurrent downloads — all within a web-based Blazor UI.

> 🔗 [Read the full blog post →](https://dev.to/aezanpathan/i-created-a-bittorrent-client-with-blazor-and-c-47i6)

---

## ⚙️ Features

- 🧾 Parses `.torrent` files (BEncoding)
- 🌐 Connects to peers via TCP
- 🧩 Multi-peer piece downloading
- 🔁 Retry logic with limits
- 🔐 SHA-1 hash validation
- 📊 Blazor UI with live progress

---

## 🚀 Getting Started

```bash
git clone https://github.com/AezanPathan/blazor-bittorrent-client.git
cd blazor-bittorrent-client
dotnet run
