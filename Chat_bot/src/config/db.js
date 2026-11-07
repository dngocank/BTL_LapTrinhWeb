const dotenv = require('dotenv');
const path = require('path');
dotenv.config({ path: path.join(__dirname, '../../.env') });

let sql;
let pool = null;
let isConnected = false;

const DB_SERVER = process.env.DB_SERVER || 'localhost';
const DB_NAME = process.env.DB_NAME || 'PhoneStoreDB';
const DB_USER = process.env.DB_USER || '';
const DB_PASSWORD = process.env.DB_PASSWORD || '';
const DB_PORT = parseInt(process.env.DB_PORT, 10) || 1433;

async function getPool() {
  if (pool && isConnected) return pool;

  try {
    // Neu co user & password -> dung driver tedious chuan
    if (DB_USER && DB_PASSWORD) {
      sql = require('mssql');
      const config = {
        user: DB_USER,
        password: DB_PASSWORD,
        server: DB_SERVER.includes('\\') ? DB_SERVER.split('\\')[0] : DB_SERVER,
        database: DB_NAME,
        port: DB_PORT,
        options: {
          instanceName: DB_SERVER.includes('\\') ? DB_SERVER.split('\\')[1] : undefined,
          encrypt: process.env.DB_ENCRYPT === 'true',
          trustServerCertificate: process.env.DB_TRUST_SERVER_CERTIFICATE !== 'false'
        }
      };
      pool = await sql.connect(config);
      isConnected = true;
      console.log(`[Database] Đã kết nối PhoneStoreDB qua SQL Auth (${DB_SERVER})`);
      return pool;
    }

    // Mặc định dùng Windows Authentication (Trusted Connection qua ODBC Driver)
    sql = require('mssql/msnodesqlv8');
    const cleanServer = DB_SERVER.replace(/\\\\+/g, '\\');
    const connStr = `Driver={ODBC Driver 17 for SQL Server};Server=${cleanServer};Database=${DB_NAME};Trusted_Connection=yes;`;
    pool = await sql.connect({ connectionString: connStr });
    isConnected = true;
    console.log(`[Database] Đã kết nối PhoneStoreDB qua Windows Auth (${cleanServer})`);
    return pool;
  } catch (err) {
    console.warn(`[Database] Chưa thể kết nối PhoneStoreDB: ${err.message}. Chuyển sang chế độ chạy an toàn (Offline cache fallback).`);
    isConnected = false;
    return null;
  }
}

async function executeQuery(queryText, params = {}) {
  try {
    const currentPool = await getPool();
    if (!currentPool) {
      throw new Error('Database not connected');
    }

    const request = currentPool.request();
    for (const [key, val] of Object.entries(params)) {
      request.input(key, val);
    }

    const result = await request.query(queryText);
    return result.recordset || [];
  } catch (error) {
    console.error(`[Database Query Error]:`, error.message);
    throw error;
  }
}

async function checkHealth() {
  try {
    const currentPool = await getPool();
    if (!currentPool) return false;
    const res = await currentPool.request().query('SELECT 1 AS Health');
    return res && res.recordset && res.recordset.length > 0;
  } catch {
    return false;
  }
}

module.exports = {
  getPool,
  executeQuery,
  checkHealth,
  isDbConnected: () => isConnected
};
