// ============================================================
// AGENDA PRO — app.js  (XAMPP + MariaDB edition)
// ============================================================

// ── CONFIGURACIÓN — cambia solo esto si es necesario ────────
const CONFIG = {
  API_BASE: 'https://localhost:44301/api',  // Puerto del backend .NET
  TIMEOUT:  8000                           // ms antes de activar demo mode
};

// ── ESTADO GLOBAL ───────────────────────────────────────────
const App = {
  currentUser : null,
  token       : null,
  isDemoMode  : false,
  events      : [],          // cache de mis eventos

  init() {
    try {
      const raw = localStorage.getItem('agendapro_auth');
      if (!raw) return;
      const auth = JSON.parse(raw);
      this.token       = auth.token  || null;
      this.currentUser = auth.user   || null;
      this.isDemoMode  = false;//auth.demo   || false;
      // if (this.isDemoMode) 
        //DemoData.activate();
    } catch { this.clearAuth(); }
  },

  save(token, user, demo = false) {
    this.token       = token;
    this.currentUser = user;
    this.isDemoMode  = demo;
    localStorage.setItem('agendapro_auth', JSON.stringify({ token, user, demo }));
  },

  clearAuth() {
    this.token = this.currentUser = null;
    this.isDemoMode = false;
    this.events = [];
    localStorage.removeItem('DemoData');
    sessionStorage.removeItem('agendapro_demo_store');
  },

  isAuth() { return !!(this.token && this.currentUser); }
};

// ── HTTP CLIENT ─────────────────────────────────────────────
const Http = {
  async req(method, path, body = null) {
    if (App.isDemoMode) throw Object.assign(new Error('DEMO'), { isDemo: true });

    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), CONFIG.TIMEOUT);

    try {
      const opts = {
        method,
        signal  : ctrl.signal,
        headers : { 'Content-Type': 'application/json' }
      };
      if (App.token) opts.headers['Authorization'] = `Bearer ${App.token}`;
      if (body)      opts.body = JSON.stringify(body);

      const res  = await fetch(`${CONFIG.API_BASE}${path}`, opts);
      clearTimeout(timer);

      if (res.status === 401) {
        App.clearAuth();
        Router.go('login');
        Toast.show('Tu sesión expiró, vuelve a ingresar.', 'info');
        throw new Error('Sesión expirada');
      }

      const json = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(json?.message || `HTTP ${res.status}`);
      return json;

    } catch (err) {
      clearTimeout(timer);
      throw err;
    }
  },

  get   : (p)    => Http.req('GET',    p),
  post  : (p, b) => Http.req('POST',   p, b),
  put   : (p, b) => Http.req('PUT',    p, b),
  delete: (p)    => Http.req('DELETE', p),
};

// ── SMART API — cae a Demo si el backend no responde ────────
const API = {
  async call(realFn, demoFn) {
    if (App.isDemoMode) return demoFn();
    try {
      return await realFn();
    } catch (err) {
      // Activar demo si es error de red / timeout / CORS / demo flag
      const networkErr = err.isDemo
        || err.name === 'AbortError'
        || err.name === 'TypeError'
        || err.message?.includes('fetch')
        || err.message?.includes('Failed to fetch')
        || err.message?.includes('NetworkError');

      // if (networkErr && !App.isDemoMode) {
      //   // App.isDemoMode = true;
      //   DemoData.activate();
      //   // Persistir modo demo en localStorage
      //   const raw  = localStorage.getItem('agendapro_auth');
      //   const auth = raw ? JSON.parse(raw) : {};
      //   auth.demo  = true;
      //   localStorage.setItem('agendapro_auth', JSON.stringify(auth));
      //   Toast.show('Backend no disponible — modo demo activado', 'warning', 6000);
      //   updateSidebarUI();
      //   return demoFn();
      // }
      if (networkErr) {
        throw new Error('No se pudo conectar con el backend');
      }
      throw err;
    }
  },

  // Auth
  login    : (d)     => API.call(() => Http.post('/auth/login',    d),  () => DemoData.login(d.email, d.password)),
  register : (d)     => API.call(() => Http.post('/auth/register', d),  () => DemoData.register(d.name, d.email)),

  // Events
  myEvents    : ()       => API.call(() => Http.get('/events/my'),           () => DemoData.myEvents()),
  dashboard   : ()       => API.call(() => Http.get('/events/dashboard'),    () => DemoData.dashboard()),
  publicEvents: ()       => API.call(() => Http.get('/events/public'),       () => DemoData.publicEvents()),
  filter      : (d, q)  => {
    let qs = '?';
    if (d) qs += `date=${encodeURIComponent(d)}&`;
    if (q) qs += `search=${encodeURIComponent(q)}`;
    return API.call(() => Http.get(`/events/filter${qs}`),   () => DemoData.filter(d, q));
  },
  create      : (d)     => API.call(() => Http.post('/events',  d),          () => DemoData.create(d)),
  update      : (id, d) => API.call(() => Http.put(`/events/${id}`, d),      () => DemoData.update(id, d)),
  remove      : (id)    => API.call(() => Http.delete(`/events/${id}`),      () => DemoData.remove(id)),
  subscribe   : (id)    => API.call(() => Http.post(`/events/${id}/subscribe`, null), () => DemoData.subscribe(id)),
  send        : (id, u) => API.call(() => Http.post(`/events/${id}/send`, { userIds: u }), () => DemoData.send(id, u)),

  // Users
  users: () => API.call(() => Http.get('/users'), () => DemoData.users()),
};

// ── TOAST ───────────────────────────────────────────────────
const Toast = {
  show(msg, type = 'info', ms = 3500) {
    const box = document.getElementById('toast-container');
    if (!box) return;
    const PAL = { success: ['✓','#52c27a'], error: ['✕','#e25252'], info: ['ℹ','#4a90e2'], warning: ['⚠','#c9a84c'] };
    const [icon, color] = PAL[type] || PAL.info;
    const el = document.createElement('div');
    el.className = `toast ${type}`;
    el.innerHTML = `<span style="color:${color};font-size:1rem;flex-shrink:0">${icon}</span><span>${msg}</span>`;
    box.appendChild(el);
    setTimeout(() => {
      el.style.cssText = 'opacity:0;transform:translateX(110%);transition:all .3s ease';
      setTimeout(() => el.remove(), 320);
    }, ms);
  }
};

// ── MODAL ───────────────────────────────────────────────────
const Modal = {
  open   : (id) => document.getElementById(id)?.classList.add('active'),
  close  : (id) => document.getElementById(id)?.classList.remove('active'),
  closeAll: ()  => document.querySelectorAll('.modal-overlay')
                             .forEach(m => m.classList.remove('active'))
};

// ── ROUTER ──────────────────────────────────────────────────
const Router = {
  go(page) {
    if (!App.isAuth() && page !== 'login' && page !== 'register') {
      Router.go('login'); return;
    }

    // Ocultar todo
    document.querySelectorAll('.page').forEach(p => p.classList.add('hidden'));
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));

    // Mostrar página destino
    document.getElementById(`page-${page}`)?.classList.remove('hidden');
    document.querySelector(`[data-page="${page}"]`)?.classList.add('active');

    // Layouts
    const isAuth = page === 'login' || page === 'register';
    document.getElementById('auth-layout')?.classList.toggle('hidden', !isAuth);
    document.getElementById('app-layout')?.classList.toggle('hidden',  isAuth);

    // Cerrar sidebar móvil
    document.getElementById('sidebar')?.classList.remove('open');

    // Título topbar
    const TITLES = {
      dashboard : 'Dashboard',
      events    : 'Mis Eventos',
      create    : 'Nuevo Evento',
      explore   : 'Explorar Eventos',
      filter    : 'Buscar Eventos',
      profile   : 'Mi Perfil'
    };
    const titleEl = document.getElementById('topbar-title');
    if (titleEl) titleEl.textContent = TITLES[page] || 'Agenda Pro';

    // Llamar al load de la página
    if (!isAuth && Pages[page]?.load) {
      Pages[page].load();
    }
  }
};

// ── FECHA UTILS ─────────────────────────────────────────────
const D = {
  fmt(iso) {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('es-ES', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  },

  toInput(iso) {
    if (!iso) return '';
    const d = new Date(iso);
    const p = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth()+1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  },

  // isOngoing: el evento está activo ahora mismo (now entre Date y EndDate)
  isOngoing(start, end) {
    const now = new Date();
    return new Date(start) <= now && new Date(end) >= now;
  },
  // isPast: el evento ya terminó (EndDate en el pasado)
  isPast(start, end) {
    return new Date(end || start) < new Date();
  },
  // isUpcoming: el evento aún no empezó
  isUpcoming: (iso) => new Date(iso) > new Date(),
  isToday(iso) {
    return new Date(iso).toDateString() === new Date().toDateString();
  },

  relative(iso) {
    const diff = new Date(iso) - new Date();
    const abs  = Math.abs(diff);
    const m = Math.floor(abs / 60_000);
    const h = Math.floor(abs / 3_600_000);
    const d = Math.floor(abs / 86_400_000);
    if (diff < 0) {
      if (m < 60)  return `hace ${m}m`;
      if (h < 24)  return `hace ${h}h`;
      return `hace ${d}d`;
    }
    if (m < 60)  return `en ${m}m`;
    if (h < 24)  return `en ${h}h`;
    if (d === 1) return 'mañana';
    return `en ${d}d`;
  }
};

// ── CARD DE EVENTO ──────────────────────────────────────────
function eventCard(ev, opts = {}) {
  const isExcl = ev.type === 'Exclusive' || ev.type === 0;

  const typeBadge = isExcl
    ? `<span class="badge badge-exclusive"><i class="fa fa-lock"></i> Exclusivo</span>`
    : `<span class="badge badge-shared"><i class="fa fa-link"></i> Compartido</span>`;

  let statusBadge = '';
  if      (D.isOngoing(ev.date, ev.endDate)) statusBadge = `<span class="badge badge-active">● En curso</span>`;
  else if (D.isToday(ev.date))               statusBadge = `<span class="badge badge-active">Hoy</span>`;
  else if (D.isPast(ev.date, ev.endDate))    statusBadge = `<span class="badge badge-past">Pasado</span>`;

  const participants = ev.participants?.length
    ? `<div class="event-meta-item"><span class="icon"><i class="fa fa-users"></i></span><span>${ev.participants.join(', ')}</span></div>` : '';

  const ownerLine = (!ev.isOwner && ev.ownerName)
    ? `<div class="event-meta-item"><span class="icon"><i class="fa fa-user"></i></span><span>Por: ${esc(ev.ownerName)}</span></div>` : '';

  const actions = opts.showActions ? `
    <div class="event-actions">
      ${opts.canEdit   ? `<button class="btn btn-secondary btn-sm" onclick="Pages.events.edit(${ev.id})">✎ Editar</button>` : ''}
      ${opts.canDelete ? `<button class="btn btn-danger btn-sm"    onclick="Pages.events.confirmDelete(${ev.id})">✕ Eliminar</button>` : ''}
      ${opts.canSend   ? `<button class="btn btn-ghost btn-sm"     onclick="Pages.events.openSend(${ev.id})">↗ Enviar</button>` : ''}
      ${opts.canAdd    ? `<button class="btn btn-primary btn-sm"   onclick="Pages.explore.add(${ev.id})">+ Agregar</button>` : ''}
    </div>` : '';

  return `
    <div class="event-card ${isExcl ? 'exclusive' : ''}">
      <div class="event-card-header">
        <span class="event-name">${esc(ev.name)}</span>
        <div style="display:flex;gap:6px;flex-wrap:wrap;justify-content:flex-end">${typeBadge}${statusBadge}</div>
      </div>
      ${ev.description ? `<p style="font-size:.82rem;color:var(--text-secondary);margin-top:6px;line-height:1.5">${esc(ev.description)}</p>` : ''}
      <div class="event-meta">
        <div class="event-meta-item">
          <i class="fa fa-calendar-alt"></i>
          <span>${D.fmt(ev.date)} → ${D.fmt(ev.endDate)}</span>
          <span style="font-size:.72rem;color:var(--text-muted);margin-left:6px">${D.relative(ev.date)}</span>
        </div>
        ${ev.location ? `<div class="event-meta-item"><span class="icon"><i class="fa fa-map-marker-alt"></i></span><span>${esc(ev.location)}</span></div>` : ''}
        ${participants}
        ${ownerLine}
      </div>
      ${actions}
    </div>`;
}

function esc(s) {
  if (!s) return '';
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ── TAG INPUT ───────────────────────────────────────────────
function renderTags(cId, tags, iId) {
  const c = document.getElementById(cId);
  if (!c) return;
  c.innerHTML = tags.map((t, i) =>
    `<span class="participant-tag">${esc(t)}<span class="remove" onclick="removeTag('${cId}','${iId}',${i})">×</span></span>`
  ).join('');
}

function addTag(cId, iId) {
  const inp = document.getElementById(iId);
  const val = inp.value.trim();
  if (!val) return;
  inp._tags = inp._tags || [];
  if (!inp._tags.includes(val)) { inp._tags.push(val); renderTags(cId, inp._tags, iId); }
  inp.value = ''; inp.focus();
}

function removeTag(cId, iId, idx) {
  const inp = document.getElementById(iId);
  if (!inp._tags) return;
  inp._tags.splice(idx, 1);
  renderTags(cId, inp._tags, iId);
}

function handleTagKeydown(e, cId, iId) {
  if (e.key === 'Enter') { e.preventDefault(); addTag(cId, iId); }
}

// ── MINI CALENDARIO ─────────────────────────────────────────
function buildCalendar(events = []) {
  const now = new Date(), y = now.getFullYear(), m = now.getMonth();
  const firstDay = new Date(y, m, 1).getDay();
  const daysInMonth = new Date(y, m + 1, 0).getDate();
  const MONTHS = ['Enero','Febrero','Marzo','Abril','Mayo','Junio',
                  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'];
  const evDays = new Set((events || []).map(e => new Date(e.date).getDate()));

  let cells = ['D','L','M','M','J','V','S']
    .map(d => `<div class="cal-day header">${d}</div>`).join('');
  for (let i = 0; i < firstDay; i++) cells += `<div class="cal-day empty"></div>`;
  for (let d = 1; d <= daysInMonth; d++) {
    const today  = d === now.getDate();
    const hasEv  = evDays.has(d) && !today;
    cells += `<div class="cal-day ${today ? 'today' : ''} ${hasEv ? 'has-event' : ''}">${d}</div>`;
  }

  return `
    <div class="mini-calendar">
      <div class="mini-calendar-header">
        <span>${MONTHS[m]} ${y}</span>
        <span style="font-size:.75rem;color:var(--text-muted)">${daysInMonth} días</span>
      </div>
      <div class="mini-calendar-grid">${cells}</div>
    </div>`;
}

// ── PÁGINAS ─────────────────────────────────────────────────
const Pages = {

  /* ── LOGIN ── */
  login: {
    load() { /* nada */ },
    async submit(e) {
      e.preventDefault();
      const btn = document.getElementById('login-btn');
      const err = document.getElementById('login-error');
      err.style.display = 'none';
      btn.disabled = true; btn.textContent = 'Ingresando…';

      try {
        const res = await API.login({
          email   : document.getElementById('login-email').value.trim(),
          password: document.getElementById('login-password').value
        });
        App.save(res.token, res.user, App.isDemoMode);
        updateSidebarUI();
        Router.go('dashboard');
        Toast.show(`Bienvenido, ${res.user.name}!${App.isDemoMode ? ' (modo demo)' : ''}`, 'success');
      } catch (ex) {
        const span = err.querySelectorAll('span')[1];
        if (span) span.textContent = ex.message || 'Credenciales incorrectas';
        err.style.display = 'flex';
      } finally {
        btn.disabled = false; btn.textContent = 'Ingresar';
      }
    }
  },

  /* ── REGISTER ── */
  register: {
    load() { /* nada */ },
    async submit(e) {
      e.preventDefault();
      const btn = document.getElementById('register-btn');
      const err = document.getElementById('register-error');
      err.style.display = 'none';

      const p1 = document.getElementById('register-password').value;
      const p2 = document.getElementById('register-password2').value;
      if (p1 !== p2) {
        const span = err.querySelectorAll('span')[1];
        if (span) span.textContent = 'Las contraseñas no coinciden';
        err.style.display = 'flex'; return;
      }

      btn.disabled = true; btn.textContent = 'Registrando…';
      try {
        const res = await API.register({
          name    : document.getElementById('register-name').value.trim(),
          email   : document.getElementById('register-email').value.trim(),
          password: p1
        });
        App.save(res.token, res.user, App.isDemoMode);
        updateSidebarUI();
        Router.go('dashboard');
        Toast.show('¡Cuenta creada exitosamente!', 'success');
      } catch (ex) {
        const span = err.querySelectorAll('span')[1];
        if (span) span.textContent = ex.message || 'Error al registrarse';
        err.style.display = 'flex';
      } finally {
        btn.disabled = false; btn.textContent = 'Registrarse';
      }
    }
  },

  /* ── DASHBOARD ── */
  dashboard: {
    async load() {
      const box = document.getElementById('dashboard-content');
      box.innerHTML = '<div class="loading-spinner"></div>';
      try {
        const data = await API.dashboard();
        this._render(data);
      } catch (ex) {
        box.innerHTML = `<div class="empty-state"><div class="icon">⚠️</div>
          <h3>Error al cargar</h3><p>${esc(ex.message)}</p></div>`;
      }
    },

    _render({ stats, ongoing, upcoming }) {
      const oCards = ongoing?.length
        ? ongoing.map(ev => eventCard(ev)).join('')
        : `<div class="empty-state"><div class="icon"><i class="fa fa-inbox"></i></div>
           <h3>Sin eventos en curso</h3></div>`;

      const uCards = upcoming?.length
        ? upcoming.map(ev => eventCard(ev)).join('')
        : `<div class="empty-state"><i class="fa fa-calendar-alt"></i>
           <h3>Sin próximos eventos</h3>
           <br><button class="btn btn-primary btn-sm" onclick="Router.go('create')">+ Crear evento</button></div>`;

      document.getElementById('dashboard-content').innerHTML = `
        <div class="stats-grid">
          <div class="stat-card"><div class="stat-icon gold"><i class="fa fa-calendar-alt"></i></div>
            <div class="stat-value">${stats?.total ?? 0}</div><div class="stat-label">Total Eventos</div></div>
          <div class="stat-card"><div class="stat-icon blue"><i class="fa fa-sync-alt"></i></div>
            <div class="stat-value">${stats?.ongoing ?? 0}</div><div class="stat-label">En Curso</div></div>
          <div class="stat-card"><div class="stat-icon green"><i class="fa fa-clock"></i></div>
            <div class="stat-value">${stats?.upcoming ?? 0}</div><div class="stat-label">Próximos</div></div>
          <div class="stat-card"><div class="stat-icon red"><i class="fa fa-lock"></i></div>
            <div class="stat-value">${stats?.exclusive ?? 0}</div><div class="stat-label">Exclusivos</div></div>
        </div>
        <div class="dashboard-grid">
          <div>
            <div class="section-header"><h2>Eventos en Curso</h2></div>
            <div class="events-grid">${oCards}</div>
            <div class="section-header" style="margin-top:32px"><h2>Próximos Eventos</h2></div>
            <div class="events-grid">${uCards}</div>
          </div>
          <div>${buildCalendar(upcoming)}</div>
        </div>`;
    }
  },

  /* ── MIS EVENTOS ── */
  events: {
    _cache  : [],
    editId  : null,
    sendId  : null,

    async load() {
      const box = document.getElementById('events-list');
      box.innerHTML = '<div class="loading-spinner"></div>';
      // Limpiar filtros al entrar
      const srch = document.getElementById('events-search');
      const filt = document.getElementById('events-type-filter');
      if (srch) srch.value = '';
      if (filt) filt.value = 'all';
      try {
        this._cache = await API.myEvents();
        App.events  = this._cache;
        this._render();
      } catch (ex) {
        box.innerHTML = `<p style="color:var(--text-muted)">Error: ${esc(ex.message)}</p>`;
      }
    },

    filterChange() { this._render(); },

    _render() {
      const search = (document.getElementById('events-search')?.value || '').toLowerCase().trim();
      const ftype  = document.getElementById('events-type-filter')?.value || 'all';
      let list = [...this._cache];

      // Filtro texto
      if (search) list = list.filter(e =>
        e.name.toLowerCase().includes(search) ||
        (e.description || '').toLowerCase().includes(search) ||
        (e.location    || '').toLowerCase().includes(search));

      // Filtro tipo/estado
      if (ftype === 'Exclusive') list = list.filter(e => e.type === 'Exclusive' || e.type === 0);
      if (ftype === 'Shared')    list = list.filter(e => e.type === 'Shared'    || e.type === 1);
      if (ftype === 'upcoming')  list = list.filter(e => D.isUpcoming(e.date));
      if (ftype === 'past')      list = list.filter(e => D.isPast(e.date, e.endDate));

      const box = document.getElementById('events-list');

      if (!list.length) {
        const isEmpty = !this._cache.length;
        box.innerHTML = `
          <div class="empty-state">
            <div class="icon">📅</div>
            <h3>${isEmpty ? 'No tienes eventos' : 'Sin resultados'}</h3>
            <p>${isEmpty ? 'Crea tu primer evento' : 'Prueba otros filtros'}</p>
            ${isEmpty ? `<br><button class="btn btn-primary" onclick="Router.go('create')">+ Nuevo Evento</button>` : ''}
          </div>`;
        return;
      }

      box.innerHTML = `
        <p style="font-size:.8rem;color:var(--text-muted);margin-bottom:16px">${list.length} evento(s)</p>
        <div class="events-grid">
          ${list.map(e => eventCard(e, {
            showActions : true,
            canEdit     : e.isOwner,
            canDelete   : e.isOwner,
            canSend     : e.isOwner && (e.type === 'Shared' || e.type === 1)
          })).join('')}
        </div>`;
    },

    edit(id) {
      const ev = this._cache.find(e => e.id === id); if (!ev) return;
      this.editId = id;

      document.getElementById('edit-name').value        = ev.name;
      document.getElementById('edit-description').value = ev.description || '';
      document.getElementById('edit-date').value        = D.toInput(ev.date);
      document.getElementById('edit-end-date').value    = D.toInput(ev.endDate);
      document.getElementById('edit-location').value    = ev.location || '';

      const isExcl = ev.type === 'Exclusive' || ev.type === 0;
      document.getElementById(`edit-type-${isExcl ? 'exclusive' : 'shared'}`).checked = true;

      const inp = document.getElementById('edit-participants-input');
      inp._tags = [...(ev.participants || [])];
      renderTags('edit-participants-tags', inp._tags, 'edit-participants-input');

      Modal.open('modal-edit');
    },

    async submitEdit() {
      const btn = document.getElementById('edit-submit-btn');
      btn.disabled = true; btn.textContent = 'Guardando…';
      const rawDate = document.getElementById('edit-date').value;
      debugger;
      const data = {
        name        : document.getElementById('edit-name').value,
        description : document.getElementById('edit-description').value,
        date        : rawDate,
        endDate     : document.getElementById('edit-end-date').value,
        location    : document.getElementById('edit-location').value,
        type        : document.querySelector('input[name="edit-type"]:checked')?.value,
        participants: document.getElementById('edit-participants-input')._tags || []
      };
      try {
        await API.update(this.editId, data);
        Toast.show('Evento actualizado', 'success');
        Modal.close('modal-edit');
        await this.load();
      } catch (ex) { Toast.show(ex.message || 'Error al actualizar', 'error'); }
      finally { btn.disabled = false; btn.textContent = 'Guardar Cambios'; }
    },

    confirmDelete(id) {
      this.editId = id;
      const ev = this._cache.find(e => e.id === id);
      const nameEl = document.getElementById('delete-event-name');
      if (nameEl && ev) nameEl.textContent = `"${ev.name}"`;
      Modal.open('modal-delete');
    },

    async deleteConfirmed() {
      try {
        await API.remove(this.editId);
        Toast.show('Evento eliminado', 'success');
        Modal.close('modal-delete');
        await this.load();
      } catch (ex) { Toast.show(ex.message || 'Error', 'error'); }
    },

    async openSend(id) {
      this.sendId = id;
      const box = document.getElementById('send-users-list');
      box.innerHTML = '<div class="loading-spinner"></div>';
      Modal.open('modal-send');
      try {
        const users    = await API.users();
        const filtered = users.filter(u => u.id !== App.currentUser?.id);
        box.innerHTML = filtered.length
          ? filtered.map(u => `
              <label class="users-checklist-item">
                <input type="checkbox" value="${u.id}">
                <div class="user-avatar" style="width:28px;height:28px;font-size:.7rem">${esc(u.name.charAt(0))}</div>
                <div>
                  <div style="font-size:.85rem;font-weight:600">${esc(u.name)}</div>
                  <div style="font-size:.75rem;color:var(--text-muted)">${esc(u.email)}</div>
                </div>
              </label>`).join('')
          : '<p style="padding:16px;color:var(--text-muted);font-size:.85rem">No hay otros usuarios disponibles</p>';
      } catch (ex) {
        box.innerHTML = `<p style="padding:16px;color:var(--text-muted)">Error: ${esc(ex.message)}</p>`;
      }
    },

    async submitSend() {
      const ids = [...document.querySelectorAll('#send-users-list input:checked')]
        .map(c => parseInt(c.value));
      if (!ids.length) { Toast.show('Selecciona al menos un usuario', 'info'); return; }
      const btn = document.querySelector('#modal-send .modal-footer .btn-primary');
      if (btn) { btn.disabled = true; btn.textContent = 'Enviando…'; }
      try {
        await API.send(this.sendId, ids);
        Toast.show(`Evento enviado a ${ids.length} usuario(s)`, 'success');
        Modal.close('modal-send');
      } catch (ex) { Toast.show(ex.message || 'Error', 'error'); }
      finally { if (btn) { btn.disabled = false; btn.textContent = '↗ Enviar'; } }
    }
  },

  /* ── CREAR EVENTO ── */
  create: {
    load() {
      document.getElementById('create-form')?.reset();
      const inp = document.getElementById('create-participants-input');
      if (inp) inp._tags = [];
      renderTags('create-participants-tags', [], 'create-participants-input');
      const err = document.getElementById('create-error');
      if (err) err.style.display = 'none';
    },

    async submit(e) {
      e.preventDefault();
      const btn = document.getElementById('create-submit-btn');
      const err = document.getElementById('create-error');
      err.style.display = 'none';
      btn.disabled = true; btn.textContent = 'Creando…';

      const data = {
        name        : document.getElementById('create-name').value,
        description : document.getElementById('create-description').value,
        date        : document.getElementById('create-date').value,
        endDate     : document.getElementById('create-end-date').value,
        location    : document.getElementById('create-location').value,
        type        : document.querySelector('input[name="create-type"]:checked')?.value || 'Shared',
        participants: document.getElementById('create-participants-input')._tags || []
      };

      try {
        await API.create(data);
        Toast.show('¡Evento creado exitosamente!', 'success');
        Router.go('events');
      } catch (ex) {
        const span = err.querySelectorAll('span')[1];
        if (span) span.textContent = ex.message || 'Error al crear';
        err.style.display = 'flex';
        err.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      } finally {
        btn.disabled = false; btn.textContent = 'Crear Evento';
      }
    }
  },

  /* ── EXPLORAR ── */
  explore: {
    async load() {
      const box = document.getElementById('explore-list');
      box.innerHTML = '<div class="loading-spinner"></div>';
      try {
        const ev = await API.publicEvents();
        if (!ev.length) {
          box.innerHTML = `<div class="empty-state"><div class="icon">🌐</div>
            <h3>No hay eventos disponibles</h3>
            <p>Todos los eventos compartidos ya están en tu agenda</p></div>`;
          return;
        }
        box.innerHTML = `
          <p style="font-size:.8rem;color:var(--text-muted);margin-bottom:16px">${ev.length} evento(s) disponible(s)</p>
          <div class="events-grid">${ev.map(e => eventCard(e, { showActions: true, canAdd: true })).join('')}</div>`;
      } catch (ex) {
        box.innerHTML = `<div class="empty-state"><div class="icon">⚠️</div>
          <h3>Error al cargar</h3><p>${esc(ex.message)}</p></div>`;
      }
    },

    async add(id) {
      try {
        await API.subscribe(id);
        Toast.show('¡Evento agregado a tu agenda!', 'success');
        await this.load();
      } catch (ex) { Toast.show(ex.message || 'Error', 'error'); }
    }
  },

  /* ── BUSCAR ── */
  filter: {
    load() {
      document.getElementById('filter-results').innerHTML = `
        <div class="empty-state">
          <div class="icon"><i class="fa fa-search"></i></span></div>
          <h3>Usa los filtros para buscar</h3>
          <p>Filtra por fecha (día completo o momento ±30 min) y/o texto libre</p>
        </div>`;
    },

    async search() {
      const date = document.getElementById('filter-date').value;
      const text = document.getElementById('filter-text').value.trim();
      if (!date && !text) { Toast.show('Ingresa al menos un filtro', 'info'); return; }

      const box = document.getElementById('filter-results');
      box.innerHTML = '<div class="loading-spinner"></div>';
      try {
        const ev = await API.filter(date, text);
        if (!ev.length) {
          box.innerHTML = `<div class="empty-state"><div class="icon"><i class="fa fa-search"></i></div>
            <h3>Sin resultados</h3><p>No se encontraron eventos con los filtros indicados</p></div>`;
          return;
        }
        box.innerHTML = `
          <p style="font-size:.82rem;color:var(--text-muted);margin-bottom:16px">${ev.length} resultado(s)</p>
          <div class="events-grid">${ev.map(e => eventCard(e)).join('')}</div>`;
      } catch (ex) {
        box.innerHTML = `<div class="empty-state"><div class="icon">⚠️</div>
          <h3>Error</h3><p>${esc(ex.message)}</p></div>`;
      }
    }
  },

  /* ── PERFIL ── */
  profile: {
    load() {
      const u = App.currentUser; if (!u) return;

      document.getElementById('profile-name').textContent  = u.name;
      document.getElementById('profile-email').textContent = u.email;
      document.getElementById('profile-avatar-big').textContent = u.name.charAt(0).toUpperCase();

      // Usar cache de eventos si existe, si no pedir del demo
      const ev = App.events.length
        ? App.events
        : (DemoData?.isActive ? DemoData.myEvents() : []);

      document.getElementById('profile-total').textContent     = ev.length;
      document.getElementById('profile-owned').textContent     = ev.filter(e => e.isOwner).length;
      document.getElementById('profile-exclusive').textContent = ev.filter(e => e.type === 'Exclusive' || e.type === 0).length;
      document.getElementById('profile-shared').textContent    = ev.filter(e => e.type === 'Shared'    || e.type === 1).length;

      const modeEl = document.getElementById('profile-mode');
      if (modeEl) {
        modeEl.textContent = App.isDemoMode ? '🎭 Modo Demo (sin backend)' : '🟢 Conectado al servidor';
        modeEl.style.color = App.isDemoMode ? 'var(--accent-gold)' : 'var(--accent-green)';
      }
    }
  }
};

// ── HELPERS DE UI ────────────────────────────────────────────
function updateSidebarUI() {
  if (!App.currentUser) return;
  const name = App.currentUser.name || 'U';
  const el = document.getElementById('sidebar-user-name'); if (el) el.textContent = name;
  const av = document.getElementById('sidebar-user-avatar'); if (av) av.textContent = name.charAt(0).toUpperCase();
  const badge = document.getElementById('demo-badge'); if (badge) badge.style.display = App.isDemoMode ? 'flex' : 'none';
}

function logout() {
  App.clearAuth();
  Router.go('login');
  Toast.show('Sesión cerrada', 'info');
}

function toggleSidebar() {
  document.getElementById('sidebar')?.classList.toggle('open');
}

// ── BOOTSTRAP ───────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  App.init();

  // Cerrar modales al hacer clic fuera
  document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', e => { if (e.target === overlay) Modal.closeAll(); });
  });

  // ESC cierra modales
  document.addEventListener('keydown', e => { if (e.key === 'Escape') Modal.closeAll(); });

  if (App.isAuth()) {
    updateSidebarUI();
    Router.go('dashboard');
  } else {
    Router.go('login');
  }
});
