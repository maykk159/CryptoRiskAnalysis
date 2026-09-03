# 📊 Crypto Risk Analysis

> **Professional Financial Risk Assessment for Cryptocurrency Assets**

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19.2-blue.svg)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.9-blue.svg)](https://www.typescriptlang.org/)

## 📖 Overview

**CryptoRiskAnalysis** is a high-performance, full-stack application designed to deliver institutional-grade risk metrics for digital assets. By leveraging a **Hybrid Data Engine**, it seamlessly aggregates real-time market data from **Binance Spot API** and **CoinGecko**, providing robust volatility tracking, trend analysis, and advanced financial indicators.

Built with **.NET 10 Clean Architecture** and a responsive **React/TypeScript** frontend, the platform ensures low-latency calculations and a premium user experience.

---

## ✨ Key Features

### 🛡️ Hybrid Data Engine
- **Smart Routing**: Intelligently routes data requests to the Binance Spot API for high-frequency assets (1-min cache) and falls back to CoinGecko for long-tail markets (3-min cache).
- **Resiliency**: Built-in circuit breakers and automatic failover mechanisms ensure continuous operation even during API outages.

### 📊 Advanced Risk Metrics
- **Volatility Analysis**: Log-returns based annualized volatility calculation for precise risk measurement.
- **Institutional Indicators**:
  - **VaR (95%)**: Value at Risk estimation for downside protection.
  - **Sharpe Ratio**: Risk-adjusted return performance evaluation.
  - **Max Drawdown**: Historical worst-case loss tracking.
  - **Downside Risk**: Focuses exclusively on negative return variance.

### ⚡ High-Performance Architecture
- **Optimized Backend**: Parallel processing and in-memory caching for sub-100ms response times.
- **Modern Frontend**: Interactive visualizations powered by Recharts and Tailwind CSS.
- **Asset Coverage**: Support for top 20 cryptocurrencies including Bitcoin (BTC), Ethereum (ETH), Solana (SOL), and more.

---

## 🛠️ Technology Stack

| Component | Technology | Description |
|-----------|------------|-------------|
| **Backend** | .NET 10 (C#) | ASP.NET Core Web API, Clean Architecture, Dependency Injection |
| **Frontend** | React 19, TypeScript | Vite, Tailwind CSS, Recharts, Axios |
| **Data Layer** | Hybrid Service | Binance API (Primary) + CoinGecko API (Fallback) |
| **Caching** | IMemoryCache | In-memory distributed caching strategy |
| **Testing** | xUnit / Jest | Comprehensive unit and integration testing capabilities |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)

> **💡 Quick Setup**: Use our [Setup Script](REQUIREMENTS.md) for dependencies.

### Installation & Execution

#### 1. Clone the Repository
```powershell
git clone https://github.com/maykk159/CryptoRiskProject.git
cd CryptoRiskProject
```

#### 2. Run Backend API
```powershell
cd CryptoRiskAnalysis.API
dotnet restore
dotnet run --project CryptoRiskAnalysis.API/CryptoRiskAnalysis.API.csproj
# API will be available at http://localhost:5058
```

#### 3. Run Frontend Application
```powershell
# Open a new terminal
cd client
npm install
npm run dev
# Application will launch at http://localhost:5173
```

---

## 📐 Risk Calculation Methodology

The platform employs industry-standard financial models adapted for the crypto market.

### Core Scoring (0-100 Scale)

1.  **Volatility Score (40%)**: Derived from annualized standard deviation of daily log returns.
    -   Formula: `σ_annual = σ_daily × √365`
2.  **Trend Score (30%)**: Momentum analysis comparing 7-day vs 30-day moving averages to detect extreme deviations.
3.  **Volume Score (30%)**: Liquidity risk assessment based on volume-to-price divergence analysis.

### Advanced Metrics

-   **Sharpe Ratio**: `(μ - r_f) / σ × √365` (using 0% risk-free rate)
-   **Downside Risk**: `√(Σ(min(r_i, 0))² / (n-1))` (Semideviation)
-   **Maximum Drawdown**: `MDD = max(1 - P_t / max(P_0...P_t))`
-   **Value at Risk (VaR 95%)**: 5th percentile of return distribution (Historical Simulation)

---

## 📂 Project Structure

```
CryptoRiskAnalysis/
├── CryptoRiskAnalysis.API/    # .NET 10 Backend Solution
│   ├── Controllers/           # REST API Endpoints
│   ├── Services/              # Business Logic & Data Services
│   ├── Models/                # Domain Entities
│   ├── DTOs/                  # Data Transfer Objects
│   └── Middleware/            # Exception Handling & Logging
│
└── client/                    # React Frontend Application
    ├── src/
    │   ├── components/        # Reusable UI Components
    │   ├── services/          # API Client Layer
    │   └── constants/         # Static Configuration
    └── public/                # Static Assets
```

---

## 🤝 Contributing

Contributions are welcome! Please follow the [Contribution Guidelines](CONTRIBUTING.md).

1.  Fork the repository
2.  Create your feature branch (`git checkout -b feature/AmazingFeature`)
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4.  Push to the branch (`git push origin feature/AmazingFeature`)
5.  Open a Pull Request

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 👤 Author

**Mayıs Kuru**
-   GitHub: [@maykk159](https://github.com/maykk159)
-   Project Link: [https://github.com/maykk159/CryptoRiskProject](https://github.com/maykk159/CryptoRiskProject)

---

> ⭐ If you find this project useful, please consider giving it a star on GitHub!
