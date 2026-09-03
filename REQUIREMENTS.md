# 🛠️ Project Requirements & Setup Guide

This document outlines the requirements to run **CryptoRiskAnalysis** on a fresh machine and provides a "One-Click" setup guide.

## ✅ Prerequisites

Before running the setup script, ensure your machine has the following tools installed:

| Tool | Version | Purpose | Download Link |
|------|---------|---------|---------------|
| **.NET SDK** | **10.0** or later | Backend Runtime | [Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Node.js** | **v18** or later | Frontend Runtime | [Download Node.js](https://nodejs.org/) |
| **Git** | Latest | Version Control | [Download Git](https://git-scm.com/) |

---

## 🚀 One-Click Setup (Windows)

We have provided a unified setup script to install all dependencies for you.

### Option 1: Run via PowerShell
1.  Open **PowerShell** in the project root folder.
2.  Run the following command:
    ```powershell
    .\setup.ps1
    ```

### Option 2: Double Click
1.  Locate `setup.ps1` in the project folder.
2.  Right-click the file and select **Run with PowerShell**.

> **What this script does:**
> 1.  Checks if `.NET 10` and `Node.js` are installed.
> 2.  Restores NuGet packages for the API.
> 3.  Installs npm packages for the React Client.

---

## 🏃‍♂️ Running the Application

After setup is complete, use these commands to start the servers:

### Terminal 1 - Backend (API)
```powershell
cd CryptoRiskAnalysis.API
dotnet run
```
_Runs on: http://localhost:5058_

### Terminal 2 - Frontend (UI)
```powershell
cd client
npm run dev
```
_Open in browser: http://localhost:5173_

---

## 🐳 Docker Support (Optional)

If you prefer using Docker, you can ignore the prerequisites above (except Docker Desktop).

*(Docker configuration coming soon)*
