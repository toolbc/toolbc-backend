# ToolBC Backend

Backend server berbasis **ASP.NET Core 8 Web API** untuk sistem manajemen perawatan pasien Tuberkulosis (ToolBC / TBC Care).

---

## 🛠️ Tech Stack & Arsitektur

- **Framework**: ASP.NET Core Web API (.NET 8.0)
- **Database & ORM**: Entity Framework Core 8 dengan dukungan **PostgreSQL (Supabase)** & **SQLite (Local Development)**
- **Authentication**: JWT (_JSON Web Tokens_) dengan Role-Based Access Control (RBAC: `Patient`, `Doctor`, `Admin`)
- **AI Engine / Chatbot**: Proxy multi-provider cerdas mendukung **OpenAI-compatible router** (seperti `9router.diwanparker.tech` / `ag/gemini-3.6-flash-medium`) dan **Google Gemini API** asli dengan mekanisme fallback otomatis
- **Documentation**: Swagger / OpenAPI UI

---

## ⚡ Cara Menjalankan

### 1. Mode Lokal (SQLite & Auto-Seed Demo)

Secara default, saat dijalankan dalam profile `http`, backend menggunakan SQLite lokal `toolbc.db` dan otomatis mengisinya dengan data contoh:

```powershell
# Masuk ke direktori backend
cd toolbc-backend

# Jalankan API
dotnet run --project .\Toolbc.Api\Toolbc.Api.csproj --launch-profile http
```

API akan aktif di:

- **Base URL**: `http://localhost:5272`
- **Swagger Documentation**: `http://localhost:5272/swagger`
- **Health Check**: `http://localhost:5272/api/health`

---

## 🤖 Konfigurasi AI Chatbot (OpenAI / Gemini)

Untuk mengamankan API key, gunakan file `appsettings.Local.json` di dalam folder `Toolbc.Api/` (file ini otomatis diabaikan oleh `.gitignore` sehingga tidak akan bocor ke GitHub):

Buat/edit `toolbc-backend/Toolbc.Api/appsettings.Local.json`:

```json
{
  "AI": {
    "Provider": "openai"
  },
  "OpenAI": {
    "Endpoint": "<YOUR_ENDPOINT_9ROUTER>",
    "Model": "ag/gemini-3.6-flash-medium",
    "ApiKey": "<YOUR_API_KEY>"
  }
}
```

---

## 🗄️ Konfigurasi Supabase PostgreSQL (Production)

Gunakan _user-secrets_ atau masukkan string koneksi Supabase di `appsettings.Local.json`:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Host=<host>;Port=6543;Database=postgres;Username=<user>;Password=<password>;Ssl Mode=Require;Trust Server Certificate=true" --project .\Toolbc.Api\Toolbc.Api.csproj
```

Jalankan migrasi database:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\Toolbc.Api\Toolbc.Api.csproj
```

---

## 📋 Endpoint Utama

| Method  | Endpoint                           | Deskripsi                                               |
| ------- | ---------------------------------- | ------------------------------------------------------- |
| `POST`  | `/api/auth/login`                  | Autentikasi pengguna & generate JWT Token               |
| `POST`  | `/api/bootstrap/admin`             | Registrasi akun admin awal (hanya aktif jika DB kosong) |
| `POST`  | `/api/chat/reply`                  | Konsultasi edukasi AI (mendukung chat history)          |
| `GET`   | `/api/patients/me/dashboard`       | Ambil data status terapi, hari aktif, & target pasien   |
| `POST`  | `/api/patients/me/medication-logs` | Pencatatan konfirmasi minum obat harian                 |
| `POST`  | `/api/patients/me/symptom-logs`    | Input checkup gejala & triage risiko otomatis           |
| `GET`   | `/api/patients/me/history`         | Riwayat kepatuhan & log pengobatan                      |
| `GET`   | `/api/doctors/me/dashboard`        | Dasbor dokter, antrian eskalasi, & pasien binaan        |
| `GET`   | `/api/doctors/me/adherence`        | Analisis kepatuhan & klaster risiko pasien              |
| `PATCH` | `/api/reminders/{id}/status`       | Update status pengingat/eskalasi pasien                 |
| `POST`  | `/api/admin/users`                 | Pembuatan akun pasien/dokter oleh administrator         |

---

## 🔑 Akun Demo Siap Pakai

| Role       | Email               | Password     |
| ---------- | ------------------- | ------------ |
| **Pasien** | `davina@pasien.com` | `Pasien123!` |
| **Dokter** | `arya@dokter.com`   | `Dokter123!` |
| **Admin**  | `admin@admin.com`   | `Admin123!`  |
