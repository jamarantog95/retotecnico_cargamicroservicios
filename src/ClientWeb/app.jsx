const { useState, useEffect, useCallback } = React;

const AUTH_API = 'http://localhost:5027';
const CONTROL_API = 'http://localhost:5299';
const TOKEN_KEY = 'control-web-token';

function formatDate(value) {
   if (!value) return '-';
   const date = new Date(value);
   return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-PE', {
      dateStyle: 'medium',
      timeStyle: 'short'
   }).format(date);
}

function statusClass(status) {
   return `status-${status.toLowerCase().replaceAll(' ', '-')}`;
}

function LoginPanel({ onLogin }) {
   const [username, setUsername] = useState('');
   const [password, setPassword] = useState('');
   const [message, setMessage] = useState('');
   const [error, setError] = useState(false);

   async function handleSubmit(event) {
      event.preventDefault();
      setError(false);
      setMessage('Validando acceso...');

      try {
         const response = await fetch(`${AUTH_API}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
         });
         const result = await response.json().catch(() => ({}));
         if (!response.ok) throw new Error(result.message || 'Las credenciales no son válidas.');

         setMessage('');
         onLogin(result.token);
      } catch (err) {
         setError(true);
         setMessage(err.message);
      }
   }

   return (
      <section className="panel login-panel" aria-labelledby="login-title">
         <div>
            <p className="eyebrow">Acceso seguro</p>
            <h2 id="login-title">Inicia sesión para continuar</h2>
         </div>
         <form onSubmit={handleSubmit}>
            <label>
               Usuario
               <input
                  name="username"
                  autoComplete="username"
                  value={username}
                  onChange={event => setUsername(event.target.value)}
                  required
               />
            </label>
            <label>
               Contraseña
               <input
                  name="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  required
               />
            </label>
            <button type="submit">Ver historial</button>
         </form>
         <p className={`message${error ? ' error' : ''}`} role="alert">{message}</p>
      </section>
   );
}

function UploadPanel({ token, onUploaded, onSessionExpired }) {
   const [file, setFile] = useState(null);
   const [progress, setProgress] = useState(0);
   const [message, setMessage] = useState('');
   const [error, setError] = useState(false);
   const [uploading, setUploading] = useState(false);

   function handleFileChange(event) {
      setFile(event.target.files[0] || null);
      setMessage('');
      setError(false);
      setProgress(0);
   }

   function uploadFile(event) {
      event.preventDefault();
      if (!file || uploading) return;

      const formData = new FormData();
      formData.append('file', file);
      const request = new XMLHttpRequest();

      setUploading(true);
      setError(false);
      setMessage('Preparando la subida...');
      setProgress(0);

      request.upload.addEventListener('progress', progressEvent => {
         if (!progressEvent.lengthComputable) return;
         const percentage = Math.round((progressEvent.loaded / progressEvent.total) * 100);
         setProgress(percentage);
         setMessage(percentage === 100 ? 'Archivo enviado. Validando el Excel...' : `Subiendo archivo... ${percentage}%`);
      });

      request.addEventListener('load', () => {
         let result = {};
         try {
            result = JSON.parse(request.responseText || '{}');
         } catch {
            result = {};
         }
         setUploading(false);

         if (request.status === 401 || request.status === 403) {
            onSessionExpired();
            return;
         }
         if (request.status < 200 || request.status >= 300) {
            setError(true);
            setMessage(result.message || 'No se pudo subir el archivo.');
            return;
         }

         setProgress(100);
         setMessage(result.message || 'Archivo subido con éxito.');
         setFile(null);
         onUploaded();
      });

      request.addEventListener('error', () => {
         setUploading(false);
         setError(true);
         setMessage('No se pudo conectar con el servicio de cargas.');
      });

      request.open('POST', `${CONTROL_API}/api/control/upload`);
      request.setRequestHeader('Authorization', `Bearer ${token}`);
      request.send(formData);
   }

   return (
      <section className="upload-panel" aria-labelledby="upload-title">
         <div>
            <p className="eyebrow">Nueva carga</p>
            <h2 id="upload-title">Sube tu archivo Excel</h2>
            <p className="upload-help">El periodo se detectará automáticamente a partir de la columna Periodo o Fecha.</p>
         </div>
         <form className="upload-form" onSubmit={uploadFile}>
            <label className="file-picker">
               <span>{file ? file.name : 'Seleccionar archivo .xlsx o .xls'}</span>
               <input type="file" accept=".xlsx,.xls" onChange={handleFileChange} disabled={uploading} />
            </label>
            <button type="submit" disabled={!file || uploading}>{uploading ? 'Subiendo...' : 'Subir archivo'}</button>
         </form>
         <div className="upload-feedback" aria-live="polite">
            <progress value={progress} max="100" hidden={!uploading && progress === 0} />
            <span className={`message${error ? ' error' : ''}`}>{message}</span>
         </div>
      </section>
   );
}

function HistoryPanel({ token, onLogout, onSessionExpired }) {
   const [items, setItems] = useState([]);
   const [message, setMessage] = useState('');
   const [error, setError] = useState(false);
   const [lastUpdated, setLastUpdated] = useState('');

   const loadHistory = useCallback(async () => {
      setError(false);
      setMessage('Cargando historial...');
      try {
         const response = await fetch(`${CONTROL_API}/api/control/history`, {
            headers: { Authorization: `Bearer ${token}` }
         });
         const result = await response.json().catch(() => ({}));

         if (response.status === 401 || response.status === 403) {
            onSessionExpired();
            return;
         }
         if (!response.ok) throw new Error(result.message || 'No se pudo consultar el historial.');

         setItems(result.data || []);
         setMessage('');
         setLastUpdated(`Actualizado ${formatDate(new Date().toISOString())}`);
      } catch (err) {
         setError(true);
         setMessage(err.message);
      }
   }, [token, onSessionExpired]);

   useEffect(() => {
      loadHistory();
   }, [loadHistory]);

   return (
      <section className="panel history-panel" aria-labelledby="history-title">
         <UploadPanel token={token} onUploaded={loadHistory} onSessionExpired={onSessionExpired} />
         <header className="history-header">
            <div>
               <p className="eyebrow">Actividad reciente</p>
               <h2 id="history-title">Cargas registradas</h2>
            </div>
            <div className="actions">
               <span className="updated">{lastUpdated}</span>
               <button className="secondary" type="button" onClick={loadHistory}>Actualizar</button>
               <button className="text-button" type="button" onClick={onLogout}>Cerrar sesión</button>
            </div>
         </header>
         <p className={`message${error ? ' error' : ''}`} role="status">{message}</p>
         <div className="table-wrap" hidden={items.length === 0}>
            <table>
               <thead>
                  <tr>
                     <th>Archivo</th>
                     <th>Periodo</th>
                     <th>Usuario</th>
                     <th>Estado</th>
                     <th>Registrada</th>
                     <th>Finalizada</th>
                  </tr>
               </thead>
               <tbody>
                  {items.map(item => (
                     <tr key={item.id}>
                        <td><strong>{item.nombreArchivo}</strong><small>#{item.id}</small></td>
                        <td>{item.periodo}</td>
                        <td>{item.usuario || '-'}</td>
                        <td><span className={`status ${statusClass(item.estado)}`}>{item.estado}</span></td>
                        <td>{formatDate(item.fechaRegistro)}</td>
                        <td>{formatDate(item.fechaFin)}</td>
                     </tr>
                  ))}
               </tbody>
            </table>
         </div>
         <div className="empty-state" hidden={items.length > 0}>
            <strong>Aún no hay cargas registradas</strong>
            <span>Cuando se procese un archivo, aparecerá aquí.</span>
         </div>
      </section>
   );
}

function App() {
   const [token, setToken] = useState(() => sessionStorage.getItem(TOKEN_KEY));

   function handleLogin(newToken) {
      sessionStorage.setItem(TOKEN_KEY, newToken);
      setToken(newToken);
   }

   function handleLogout() {
      sessionStorage.removeItem(TOKEN_KEY);
      setToken(null);
   }

   return (
      <main className="shell">
         <section className="intro">
            <p className="eyebrow">Control de archivos</p>
            <h1>Historial de cargas</h1>
            <p className="lede">Consulta el estado de los archivos enviados al proceso de datos.</p>
         </section>

         {token
            ? <HistoryPanel token={token} onLogout={handleLogout} onSessionExpired={handleLogout} />
            : <LoginPanel onLogin={handleLogin} />}
      </main>
   );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
