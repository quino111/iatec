// ============================================================
// AGENDA PRO — demo.js
// Datos en memoria para funcionar sin backend.
// Se activa automáticamente si el servidor no responde.
// ============================================================

const DemoData = (() => {
  const now = () => new Date();
  const h   = (hrs)  => new Date(Date.now() + hrs  * 3_600_000).toISOString();
  const d   = (days) => new Date(Date.now() + days * 86_400_000).toISOString();

  // ── Usuarios predefinidos ──────────────────────────────────
  const USERS = [
    { id: 1, name: 'Administrador IATec', email: 'admin@iatec.com'  },
    { id: 2, name: 'Juan Pérez',          email: 'juan@iatec.com'   },
    { id: 3, name: 'María García',        email: 'maria@iatec.com'  },
    { id: 4, name: 'Carlos López',        email: 'carlos@iatec.com' },
    { id: 5, name: 'Ana Martínez',        email: 'ana@iatec.com'    },
  ];

  // ── Eventos base ───────────────────────────────────────────
  const BASE_EVENTS = [
    { id:1,  name:'Ejemplo IATec',   description:'Reunión inicial del proyecto de agenda',           date:h(-2),   location:'Sala de Conferencias A',      type:'Exclusive', ownerId:1, ownerName:'Administrador IATec', participants:['Juan Pérez','María García','Carlos López'], isOwner:true  },
    // { id:2,  name:'Revisión de Requisitos',   description:'Análisis detallado de requisitos del cliente',    date:d(1),    location:'Zoom — Link por correo',      type:'Shared',    ownerId:1, ownerName:'Administrador IATec', participants:['Cliente IATec','Equipo Dev'],                isOwner:true  },
    // { id:3,  name:'Presentación al Cliente',  description:'Demo final del sistema de agenda',                date:d(5),    location:'Oficinas del Cliente, Piso 3',type:'Exclusive', ownerId:1, ownerName:'Administrador IATec', participants:['Director de Proyectos'],                    isOwner:true  },
    // { id:4,  name:'Capacitación de Usuarios', description:'Entrenamiento del equipo en el nuevo sistema',   date:d(7),    location:'Aula de Formación B',         type:'Shared',    ownerId:1, ownerName:'Administrador IATec', participants:['Todo el equipo'],                           isOwner:true  },
    // { id:5,  name:'Retrospectiva Sprint 1',   description:'Review del primer sprint de desarrollo',         date:d(-5),   location:'Sala Agile',                  type:'Exclusive', ownerId:1, ownerName:'Administrador IATec', participants:[],                                           isOwner:true  },
    // { id:6,  name:'Stand-up Diario',          description:'Daily standup del equipo de desarrollo',         date:h(-0.3), location:'Microsoft Teams',             type:'Shared',    ownerId:2, ownerName:'Juan Pérez',          participants:['Equipo dev'],                               isOwner:false },
    // { id:7,  name:'Almuerzo de Equipo',       description:'Almuerzo mensual de integración del equipo',     date:d(3),    location:'Restaurante La Plaza',        type:'Shared',    ownerId:2, ownerName:'Juan Pérez',          participants:['Todo el equipo'],                           isOwner:false },
    // { id:8,  name:'Integración de APIs',      description:'Sesión de integración frontend-backend',         date:h(3),    location:'Sala Técnica',                type:'Shared',    ownerId:4, ownerName:'Carlos López',        participants:['Frontend Team','Backend Team'],              isOwner:false },
    // { id:9,  name:'Planificación Sprint 2',   description:'Planning del segundo sprint de desarrollo',      date:d(10),   location:'Sala Agile',                  type:'Shared',    ownerId:5, ownerName:'Ana Martínez',        participants:['Scrum Master','Dev Team'],                  isOwner:false },
  ];

  const PUBLIC_EXTRA = [
    { id:20, name:'Meetup JavaScript Bolivia', description:'Encuentro mensual de desarrolladores JS', date:d(4),  location:'Centro de Convenciones', type:'Shared', ownerId:3, ownerName:'María García',  participants:['Comunidad JS'],    isOwner:false },
    { id:21, name:'Workshop Docker & K8s',      description:'Taller práctico de contenedores',       date:d(6),  location:'Universidad Técnica',   type:'Shared', ownerId:4, ownerName:'Carlos López', participants:['DevOps Team'],     isOwner:false },
    { id:22, name:'Conferencia .NET Latam',     description:'Conferencia regional de .NET y C#',     date:d(12), location:'Auditorio Principal',   type:'Shared', ownerId:5, ownerName:'Ana Martínez', participants:['Comunidad .NET'],  isOwner:false },
  ];

  let _nextId = 100;

  // ── Store persistente en sessionStorage ───────────────────
  const STORE_KEY = 'agendapro_demo_store';

  function loadStore() {
    try {
      const raw = sessionStorage.getItem(STORE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  }

  function saveStore(s) {
    try { sessionStorage.setItem(STORE_KEY, JSON.stringify(s)); } catch { /* ignore */ }
  }

  function getStore() {
    let s = loadStore();
    if (!s) {
      s = { events: [...BASE_EVENTS], publicExtra: [...PUBLIC_EXTRA], nextId: _nextId };
      saveStore(s);
    }
    return s;
  }

  function mutate(fn) {
    const s = getStore();
    fn(s);
    saveStore(s);
    return s;
  }

  // ── helpers de fecha ──────────────────────────────────────
  const isOngoing   = iso => { const t=new Date(iso),n=new Date(); return t<=n && t>=new Date(n-3*3_600_000); };
  const isUpcoming  = iso => new Date(iso) > new Date();
  const isPast      = iso => new Date(iso) < new Date();
  const sameDay     = (a,b) => new Date(a).toDateString() === new Date(b).toDateString();

  // ── validar superposición de exclusivos ───────────────────
  function checkExclusiveOverlap(events, date, excludeId = null) {
    const conflict = events.find(e =>
      (e.type === 'Exclusive' || e.type === 0) &&
      sameDay(e.date, date) &&
      e.id !== excludeId
    );
    if (conflict) {
      throw new Error(`Ya tienes el evento exclusivo "${conflict.name}" en esa fecha. Los eventos exclusivos no pueden coincidir en el mismo día.`);
    }
  }

  // ── currentUserId ─────────────────────────────────────────
  // Se inyecta desde App.currentUser al activar demo
  let _currentUserId = 1;

  return {
    isActive: false,

    activate() {
      this.isActive = true;
      _currentUserId = App?.currentUser?.id || 1;
      console.info('🎭 Demo mode ON — datos en memoria');
    },

    // ── Auth ──────────────────────────────────────────────
    login(email, password) {
      const u = USERS.find(x => x.email === email);
      if (!u) throw new Error('Usuario no encontrado en el modo demo');
      _currentUserId = u.id;
      return { token: `demo-${u.id}-${Date.now()}`, user: u };
    },

    register(name, email) {
      const s = getStore();
      const id = s.nextId++;
      const u  = { id, name, email };
      USERS.push(u);
      saveStore(s);
      _currentUserId = id;
      return { token: `demo-${id}-${Date.now()}`, user: u };
    },

    // ── Events ────────────────────────────────────────────
    myEvents() {
      return [...getStore().events].sort((a, b) => new Date(a.date) - new Date(b.date));
    },

    dashboard() {
      const all      = getStore().events;
      const ongoing  = all.filter(e => isOngoing(e.date));
      const upcoming = all.filter(e => isUpcoming(e.date))
                          .sort((a, b) => new Date(a.date) - new Date(b.date))
                          .slice(0, 8);
      return {
        stats: {
          total     : all.length,
          ongoing   : ongoing.length,
          upcoming  : upcoming.length,
          exclusive : all.filter(e => e.type === 'Exclusive' || e.type === 0).length
        },
        ongoing,
        upcoming
      };
    },

    publicEvents() {
      const myIds = new Set(getStore().events.map(e => e.id));
      return getStore().publicExtra.filter(e => !myIds.has(e.id));
    },

    filter(dateStr, text) {
      let list = [...getStore().events];

      if (dateStr) {
        const fd      = new Date(dateStr);
        const hasTime = dateStr.includes('T') && dateStr.slice(11,16) !== '00:00';
        if (hasTime) {
          const from = new Date(fd - 30 * 60_000);
          const to   = new Date(fd + 30 * 60_000);
          list = list.filter(e => { const t = new Date(e.date); return t >= from && t <= to; });
        } else {
          list = list.filter(e => sameDay(e.date, dateStr));
        }
      }

      if (text) {
        const q = text.toLowerCase();
        list = list.filter(e =>
          e.name.toLowerCase().includes(q) ||
          (e.description || '').toLowerCase().includes(q) ||
          (e.location    || '').toLowerCase().includes(q));
      }

      return list.sort((a, b) => new Date(a.date) - new Date(b.date));
    },

    create(data) {
      const s = getStore();
      if (data.type === 'Exclusive') checkExclusiveOverlap(s.events, data.date);

      const ev = {
        id          : s.nextId++,
        name        : data.name,
        description : data.description || '',
        date        : data.date,
        location    : data.location || '',
        type        : data.type || 'Shared',
        ownerId     : _currentUserId,
        ownerName   : App?.currentUser?.name || 'Demo User',
        participants: data.participants || [],
        isOwner     : true,
        createdAt   : new Date().toISOString()
      };
      s.events.push(ev);
      saveStore(s);
      return ev;
    },

    update(id, data) {
      return mutate(s => {
        const idx = s.events.findIndex(e => e.id === id && e.isOwner);
        if (idx === -1) throw new Error('Evento no encontrado o sin permisos');
        if (data.type === 'Exclusive') checkExclusiveOverlap(s.events, data.date, id);
        s.events[idx] = {
          ...s.events[idx],
          name        : data.name,
          description : data.description || '',
          date        : data.date,
          location    : data.location || '',
          type        : data.type,
          participants: data.participants || []
        };
      }).events.find(e => e.id === id);
    },

    remove(id) {
      mutate(s => {
        const idx = s.events.findIndex(e => e.id === id && e.isOwner);
        if (idx === -1) throw new Error('Evento no encontrado o sin permisos');
        s.events.splice(idx, 1);
      });
    },

    subscribe(id) {
      const pub = getStore().publicExtra;
      const ev  = pub.find(e => e.id === id);
      if (!ev) throw new Error('Evento no disponible o ya agregado');
      mutate(s => {
        if (!s.events.find(e => e.id === id)) {
          s.events.push({ ...ev, isOwner: false });
        }
      });
    },

    send(eventId, userIds) {
      // En demo sólo simula el envío
      return { success: true };
    },

    users() {
      return USERS.filter(u => u.id !== _currentUserId);
    }
  };
})();
