import { useEffect, useState } from 'react';
import { Link, Route, Routes, useNavigate } from 'react-router-dom';

const API_BASE = import.meta.env.VITE_API_BASE || '/api/v1';

function getStoredAuth() {
  const raw = localStorage.getItem('auth');
  return raw ? JSON.parse(raw) : null;
}

function setStoredAuth(auth) {
  if (auth) localStorage.setItem('auth', JSON.stringify(auth));
  else localStorage.removeItem('auth');
}

function parseTokenPayload(token) {
  try {
    const payload = token.split('.')[1];
    return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
  } catch {
    return {};
  }
}

function isAdminUser(auth) {
  if (!auth?.access_token) return false;
  const payload = parseTokenPayload(auth.access_token);
  return payload.role === 'Admin'
    || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] === 'Admin';
}

async function apiFetch(path, auth, options = {}) {
  const headers = {
    'Content-Type': 'application/json',
    ...(options.headers || {})
  };
  if (auth?.access_token) headers.Authorization = `Bearer ${auth.access_token}`;

  const response = await fetch(`${API_BASE}${path}`, { ...options, headers });
  if (response.status === 401) throw new Error('Unauthorized');
  if (!response.ok) {
    const text = await response.text();
    try {
      const body = JSON.parse(text);
      throw new Error(body.message || body.error || text);
    } catch (parseError) {
      if (parseError instanceof Error && parseError.message !== text) throw parseError;
      throw new Error(text || `Request failed: ${response.status}`);
    }
  }
  if (response.status === 204) return null;
  return response.json();
}

function Layout({ auth, onLogout, children }) {
  const isAdmin = isAdminUser(auth);

  return (
    <div className="app">
      <header className="header">
        <div>
          <h1>Flight Booking</h1>
          <p className="subtitle">Поиск и покупка авиабилетов</p>
        </div>
        <nav>
          {auth ? (
            <>
              <Link to="/">Рейсы</Link>
              <Link to="/tickets">Билеты</Link>
              <Link to="/privilege">Бонусы</Link>
              <Link to="/profile">Профиль</Link>
              {isAdmin && <Link to="/admin">Статистика</Link>}
              {isAdmin && <Link to="/admin/flights">Управление рейсами</Link>}
              {isAdmin && <Link to="/admin/users">Пользователи</Link>}
              <button className="link-button" onClick={onLogout}>Выйти</button>
            </>
          ) : (
            <a href={`${API_BASE}/authorize`}>Войти</a>
          )}
        </nav>
      </header>
      <main>{children}</main>
    </div>
  );
}

function LoginPage() {
  const navigate = useNavigate();
  const params = new URLSearchParams(window.location.search);
  const oauthParams = {
    response_type: params.get('response_type') || 'code',
    client_id: params.get('client_id') || '',
    redirect_uri: params.get('redirect_uri') || '',
    scope: params.get('scope') || 'openid profile email',
    state: params.get('state') || undefined
  };
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (!oauthParams.client_id || !oauthParams.redirect_uri)
      navigate('/');
  }, [navigate, oauthParams.client_id, oauthParams.redirect_uri]);

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');

    try {
      const response = await fetch(`${API_BASE}/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...oauthParams,
          username,
          password
        })
      });

      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(
          body.error === 'invalid_credentials'
            ? 'Неверный логин или пароль'
            : body.message || body.error || 'Ошибка входа'
        );
        return;
      }

      if (!body.redirectUrl) {
        setError('IDP не вернул redirectUrl');
        return;
      }

      window.location.href = body.redirectUrl;
    } catch {
      setError('Не удалось связаться с сервером авторизации');
    }
  }

  if (!oauthParams.client_id || !oauthParams.redirect_uri)
    return null;

  return (
    <section className="login-page">
      <form className="user-form login-form" onSubmit={handleSubmit}>
        <h2>Вход в Flight Booking</h2>
        {error && <p className="error">{error}</p>}
        <label>
          Логин
          <input
            required
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
        </label>
        <label>
          Пароль
          <input
            required
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        <button type="submit">Войти</button>
      </form>
    </section>
  );
}

function CallbackPage({ onAuth }) {
  const navigate = useNavigate();

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const tokenPayload = params.get('token');
    if (!tokenPayload) {
      navigate('/');
      return;
    }

    try {
      const auth = JSON.parse(decodeURIComponent(tokenPayload));
      onAuth(auth);
      navigate('/');
    } catch {
      navigate('/');
    }
  }, [navigate, onAuth]);

  return <p className="info">Завершение авторизации...</p>;
}

function FlightsPage({ auth }) {
  const [flights, setFlights] = useState(null);
  const [error, setError] = useState('');
  const [paidFromBalance, setPaidFromBalance] = useState(false);

  useEffect(() => {
    if (!auth) return;
    apiFetch('/flights?page=1&size=20', auth)
      .then(setFlights)
      .catch((e) => setError(e.message));
  }, [auth]);

  async function buy(flight) {
    try {
      await apiFetch('/tickets', auth, {
        method: 'POST',
        body: JSON.stringify({
          flightNumber: flight.flightNumber,
          price: flight.price,
          paidFromBalance
        })
      });
      alert('Билет успешно куплен');
      const updated = await apiFetch('/flights?page=1&size=20', auth);
      setFlights(updated);
    } catch (e) {
      alert(e.message);
    }
  }

  if (!auth) return <p className="info">Войдите, чтобы просматривать рейсы.</p>;
  if (error) return <p className="error">{error}</p>;
  if (!flights) return <p className="info">Загрузка рейсов...</p>;

  return (
    <section>
      <h2>Доступные рейсы</h2>
      <label className="checkbox">
        <input type="checkbox" checked={paidFromBalance} onChange={(e) => setPaidFromBalance(e.target.checked)} />
        Оплатить бонусами (если доступно)
      </label>
      <div className="cards">
        {flights.items?.map((flight) => {
          const capacity = flight.capacity ?? 100;
          const bought = flight.bought ?? 0;
          const free = Math.max(0, capacity - bought);
          return (
            <article key={flight.flightNumber} className="card">
              <h3>{flight.flightNumber}</h3>
              <p>{flight.fromAirport} → {flight.toAirport}</p>
              <p>{flight.date}</p>
              <p className="price">{flight.price} ₽</p>
              <p>Свободно {free}/{capacity}</p>
              <button onClick={() => buy(flight)}>Купить</button>
            </article>
          );
        })}
      </div>
    </section>
  );
}

function TicketsPage({ auth }) {
  const [tickets, setTickets] = useState([]);

  useEffect(() => {
    if (!auth) return;
    apiFetch('/tickets', auth).then(setTickets).catch(console.error);
  }, [auth]);

  async function cancel(uid) {
    await apiFetch(`/tickets/${uid}`, auth, { method: 'DELETE' });
    setTickets((prev) => prev.filter((t) => t.ticketUid !== uid));
  }

  if (!auth) return <p className="info">Войдите, чтобы просмотреть билеты.</p>;

  return (
    <section>
      <h2>Мои билеты</h2>
      <div className="cards">
        {tickets.map((ticket) => (
          <article key={ticket.ticketUid} className="card">
            <h3>{ticket.flightNumber}</h3>
            <p>{ticket.fromAirport} → {ticket.toAirport}</p>
            <p>{ticket.date}</p>
            <p>Статус: {ticket.status}</p>
            {ticket.status === 'PAID' && (
              <button onClick={() => cancel(ticket.ticketUid)}>Вернуть</button>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

function PrivilegePage({ auth }) {
  const [privilege, setPrivilege] = useState(null);

  useEffect(() => {
    if (!auth) return;
    apiFetch('/privilege', auth).then(setPrivilege).catch(console.error);
  }, [auth]);

  if (!auth) return <p className="info">Войдите, чтобы просмотреть бонусы.</p>;
  if (!privilege) return <p className="info">Загрузка...</p>;

  return (
    <section>
      <h2>Бонусный счёт</h2>
      <div className="card">
        <p>Баланс: {privilege.balance}</p>
        <p>Статус: {privilege.status}</p>
      </div>
      <h3>История</h3>
      <ul className="history">
        {privilege.history?.map((item, index) => (
          <li key={index}>
            {new Date(item.date).toLocaleString()} — {item.operationType}: {item.balanceDiff}
          </li>
        ))}
      </ul>
    </section>
  );
}

function ProfilePage({ auth }) {
  const [profile, setProfile] = useState(null);

  useEffect(() => {
    if (!auth) return;
    apiFetch('/me', auth).then(setProfile).catch(console.error);
  }, [auth]);

  if (!auth) return <p className="info">Войдите, чтобы просмотреть профиль.</p>;
  if (!profile) return <p className="info">Загрузка...</p>;

  return (
    <section>
      <h2>Профиль</h2>
      <p>Билетов: {profile.tickets?.length || 0}</p>
      <p>Бонусный баланс: {profile.privilege != null ? profile.privilege.balance : 'N/A'}</p>
    </section>
  );
}

function AdminPage({ auth }) {
  const [report, setReport] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!auth) return;
    setError('');
    setReport(null);
    apiFetch('/statistics/report', auth)
      .then(setReport)
      .catch((e) => setError(e.message));
  }, [auth]);

  if (error) return <p className="error">{error}</p>;
  if (!report) return <p className="info">Загрузка отчёта...</p>;

  return (
    <section>
      <h2>Отчёт статистики</h2>
      <p>Всего событий: {report.totalEvents}</p>
      <div className="grid">
        <div>
          <h3>События по часам</h3>
          <ul>
            {report.load?.eventsByHour?.map((x) => (
              <li key={x.hour}>{x.hour}: {x.count}</li>
            ))}
          </ul>
        </div>
        <div>
          <h3>По пользователям</h3>
          <ul>
            {report.byUser?.map((x) => (
              <li key={x.name}>{x.name}: {x.count}</li>
            ))}
          </ul>
        </div>
        <div>
          <h3>По действиям</h3>
          <ul>
            {report.byAction?.map((x) => (
              <li key={x.name}>{x.name}: {x.count}</li>
            ))}
          </ul>
        </div>
        <div>
          <h3>По сервисам</h3>
          <ul>
            {report.byService?.map((x) => (
              <li key={x.name}>{x.name}: {x.count}</li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function AdminFlightsPage({ auth }) {
  const [airports, setAirports] = useState([]);
  const [flights, setFlights] = useState([]);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [airportForm, setAirportForm] = useState({ name: '', city: '', country: '' });
  const [flightForm, setFlightForm] = useState({
    flightNumber: '',
    date: '',
    time: '',
    fromAirportId: '',
    toAirportId: '',
    price: '',
    capacity: '100'
  });

  async function reload() {
    const [airportList, flightPage] = await Promise.all([
      apiFetch('/airports', auth),
      apiFetch('/flights?page=1&size=50', auth)
    ]);
    setAirports(airportList);
    setFlights(flightPage.items || []);
  }

  useEffect(() => {
    if (!auth) return;
    reload().catch((e) => setError(e.message));
  }, [auth]);

  async function createAirport(e) {
    e.preventDefault();
    setError('');
    setMessage('');
    try {
      await apiFetch('/airports', auth, {
        method: 'POST',
        body: JSON.stringify(airportForm)
      });
      setAirportForm({ name: '', city: '', country: '' });
      setMessage('Аэропорт создан');
      await reload();
    } catch (err) {
      setError(err.message);
    }
  }

  async function createFlight(e) {
    e.preventDefault();
    setError('');
    setMessage('');
    try {
      await apiFetch('/flights', auth, {
        method: 'POST',
        body: JSON.stringify({
          flightNumber: flightForm.flightNumber,
          dateTime: `${flightForm.date}T${flightForm.time}:00Z`,
          fromAirportId: Number(flightForm.fromAirportId),
          toAirportId: Number(flightForm.toAirportId),
          price: Number(flightForm.price),
          capacity: Number(flightForm.capacity)
        })
      });
      setFlightForm({
        flightNumber: '',
        date: '',
        time: '',
        fromAirportId: '',
        toAirportId: '',
        price: '',
        capacity: '100'
      });
      setMessage('Рейс создан');
      await reload();
    } catch (err) {
      setError(err.message);
    }
  }

  if (!auth) return <p className="info">Войдите как администратор.</p>;

  return (
    <section>
      <h2>Управление рейсами</h2>
      {message && <p className="info">{message}</p>}
      {error && <p className="error">{error}</p>}

      <div className="admin-grid">
        <form className="user-form" onSubmit={createAirport}>
          <h3>Создать аэропорт</h3>
          <label>Название<input required value={airportForm.name} onChange={(e) => setAirportForm({ ...airportForm, name: e.target.value })} /></label>
          <label>Город<input required value={airportForm.city} onChange={(e) => setAirportForm({ ...airportForm, city: e.target.value })} /></label>
          <label>Страна<input required value={airportForm.country} onChange={(e) => setAirportForm({ ...airportForm, country: e.target.value })} /></label>
          <button type="submit">Создать аэропорт</button>
        </form>

        <form className="user-form" onSubmit={createFlight}>
          <h3>Создать рейс</h3>
          <label>Номер<input required value={flightForm.flightNumber} onChange={(e) => setFlightForm({ ...flightForm, flightNumber: e.target.value })} /></label>
          <label>Дата<input required type="date" value={flightForm.date} onChange={(e) => setFlightForm({ ...flightForm, date: e.target.value })} /></label>
          <label>Время<input required type="time" value={flightForm.time} onChange={(e) => setFlightForm({ ...flightForm, time: e.target.value })} /></label>
          <label>
            Откуда
            <select required value={flightForm.fromAirportId} onChange={(e) => setFlightForm({ ...flightForm, fromAirportId: e.target.value })}>
              <option value="">Выберите</option>
              {airports.map((a) => <option key={a.id} value={a.id}>{a.id} — {a.city}, {a.name}</option>)}
            </select>
          </label>
          <label>
            Куда
            <select required value={flightForm.toAirportId} onChange={(e) => setFlightForm({ ...flightForm, toAirportId: e.target.value })}>
              <option value="">Выберите</option>
              {airports.map((a) => <option key={a.id} value={a.id}>{a.id} — {a.city}, {a.name}</option>)}
            </select>
          </label>
          <label>Цена<input required type="number" min="1" value={flightForm.price} onChange={(e) => setFlightForm({ ...flightForm, price: e.target.value })} /></label>
          <label>Места<input required type="number" min="1" value={flightForm.capacity} onChange={(e) => setFlightForm({ ...flightForm, capacity: e.target.value })} /></label>
          <button type="submit" disabled={airports.length < 2}>Создать рейс</button>
        </form>
      </div>

      <div className="admin-grid">
        <div className="card">
          <h3>Аэропорты</h3>
          <div className="table-wrap">
            <table>
              <thead><tr><th>ID</th><th>Название</th><th>Город</th><th>Страна</th></tr></thead>
              <tbody>
                {airports.map((a) => (
                  <tr key={a.id}><td>{a.id}</td><td>{a.name}</td><td>{a.city}</td><td>{a.country}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
        <div className="card">
          <h3>Рейсы</h3>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Рейс</th><th>Откуда</th><th>Куда</th><th>Дата</th><th>Цена</th><th>Места</th></tr></thead>
              <tbody>
                {flights.map((f) => (
                  <tr key={f.flightNumber}>
                    <td>{f.flightNumber}</td>
                    <td>{f.fromAirport}</td>
                    <td>{f.toAirport}</td>
                    <td>{f.date}</td>
                    <td>{f.price}</td>
                    <td>{f.capacity ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>
  );
}

function AdminUsersPage({ auth }) {
  const [users, setUsers] = useState([]);
  const [error, setError] = useState('');
  const [form, setForm] = useState({
    username: '',
    password: '',
    email: '',
    firstName: '',
    lastName: ''
  });
  const [message, setMessage] = useState('');

  async function loadUsers() {
    const list = await apiFetch('/users', auth);
    setUsers(list);
  }

  useEffect(() => {
    if (!auth) return;
    loadUsers().catch((e) => setError(e.message));
  }, [auth]);

  async function createUser(e) {
    e.preventDefault();
    setMessage('');
    setError('');
    try {
      await apiFetch('/users', auth, {
        method: 'POST',
        body: JSON.stringify(form)
      });
      setForm({ username: '', password: '', email: '', firstName: '', lastName: '' });
      setMessage('Пользователь создан');
      await loadUsers();
    } catch (err) {
      setError(err.message);
    }
  }

  if (!auth) return <p className="info">Войдите как администратор.</p>;
  if (error && users.length === 0) return <p className="error">{error}</p>;

  return (
    <section>
      <h2>Пользователи</h2>
      {message && <p className="info">{message}</p>}
      {error && <p className="error">{error}</p>}

      <form className="user-form" onSubmit={createUser}>
        <h3>Создать пользователя</h3>
        <label>
          Логин
          <input required value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} />
        </label>
        <label>
          Пароль
          <input required type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
        </label>
        <label>
          Email
          <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
        </label>
        <label>
          Имя
          <input value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
        </label>
        <label>
          Фамилия
          <input value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
        </label>
        <button type="submit">Создать</button>
      </form>

      <div className="cards">
        {users.map((user) => (
          <article key={user.username} className="card">
            <h3>{user.username}</h3>
            <p>Роль: {user.role}</p>
            <p>{user.email || '—'}</p>
            <p>{[user.firstName, user.lastName].filter(Boolean).join(' ') || '—'}</p>
          </article>
        ))}
      </div>
    </section>
  );
}

export default function App() {
  const [auth, setAuth] = useState(getStoredAuth());
  const navigate = useNavigate();

  const handleAuth = (value) => {
    setStoredAuth(value);
    setAuth(value);
  };

  const handleLogout = () => {
    setStoredAuth(null);
    setAuth(null);
    navigate('/');
  };

  return (
    <Layout auth={auth} onLogout={handleLogout}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/callback" element={<CallbackPage onAuth={handleAuth} />} />
        <Route path="/" element={<FlightsPage auth={auth} />} />
        <Route path="/tickets" element={<TicketsPage auth={auth} />} />
        <Route path="/privilege" element={<PrivilegePage auth={auth} />} />
        <Route path="/profile" element={<ProfilePage auth={auth} />} />
        <Route path="/admin" element={<AdminPage auth={auth} />} />
        <Route path="/admin/flights" element={<AdminFlightsPage auth={auth} />} />
        <Route path="/admin/users" element={<AdminUsersPage auth={auth} />} />
      </Routes>
    </Layout>
  );
}
