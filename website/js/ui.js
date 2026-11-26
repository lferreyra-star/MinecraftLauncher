/* XD Launcher · interacciones visuales (independientes de Firebase) */
(() => {
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
  let reduce = motionQuery.matches;
  const onMotionChange = (e) => { reduce = e.matches; };
  if (motionQuery.addEventListener) motionQuery.addEventListener('change', onMotionChange);
  else if (motionQuery.addListener) motionQuery.addListener(onMotionChange);

  /* ---------- navegación, progreso y sección activa ---------- */
  const nav = $('#nav');
  const bar = $('#page-progress-bar');
  const toTop = $('#to-top');
  const navLinks = $$('#nav-links a[href^="#"]');
  const trackedSections = navLinks
    .map((a) => document.getElementById(a.getAttribute('href').slice(1)))
    .filter(Boolean);

  let scrollQueued = false;
  function updateScroll() {
    scrollQueued = false;
    const y = window.scrollY || document.documentElement.scrollTop;
    const max = document.documentElement.scrollHeight - window.innerHeight;
    if (nav) nav.classList.toggle('scrolled', y > 18);
    if (bar) bar.style.transform = `scaleX(${max > 0 ? Math.min(y / max, 1) : 0})`;
    if (toTop) toTop.classList.toggle('show', y > 520);

    let active = null;
    for (const section of trackedSections) {
      if (section.getBoundingClientRect().top <= 150) active = section.id;
    }
    navLinks.forEach((a) => a.classList.toggle('active', a.getAttribute('href') === `#${active}`));
  }
  const onScroll = () => {
    if (scrollQueued) return;
    scrollQueued = true;
    requestAnimationFrame(updateScroll);
  };
  window.addEventListener('scroll', onScroll, { passive: true });
  window.addEventListener('resize', onScroll, { passive: true });
  updateScroll();

  const toggle = $('#nav-toggle');
  const links = $('#nav-links');
  const closeMenu = () => {
    if (!links || !toggle) return;
    links.classList.remove('open');
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-label', 'Abrir menú');
  };
  if (toggle && links) {
    toggle.addEventListener('click', () => {
      const open = links.classList.toggle('open');
      toggle.setAttribute('aria-expanded', String(open));
      toggle.setAttribute('aria-label', open ? 'Cerrar menú' : 'Abrir menú');
    });
    $$('a', links).forEach((a) => a.addEventListener('click', closeMenu));
    document.addEventListener('click', (e) => {
      if (links.classList.contains('open') && !links.contains(e.target) && !toggle.contains(e.target)) closeMenu();
    });
  }
  window.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeMenu(); });
  toTop && toTop.addEventListener('click', () => window.scrollTo({ top: 0, behavior: reduce ? 'auto' : 'smooth' }));

  /* ---------- aparición progresiva ---------- */
  const revealEls = $$('.reveal');
  const pending = new Set(revealEls);
  const show = (el) => { el.classList.add('visible'); pending.delete(el); };
  let revealIO = null;
  if ('IntersectionObserver' in window) {
    revealIO = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) { show(entry.target); revealIO.unobserve(entry.target); }
      });
    }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });
    revealEls.forEach((el) => revealIO.observe(el));
  } else {
    revealEls.forEach(show);
  }
  const revealFallback = () => {
    pending.forEach((el) => {
      const r = el.getBoundingClientRect();
      if (r.top < window.innerHeight - 20) {
        show(el);
        revealIO && revealIO.unobserve(el);
      }
    });
  };
  window.addEventListener('scroll', () => requestAnimationFrame(revealFallback), { passive: true });
  revealFallback();

  /* ---------- contadores ---------- */
  const counters = $$('.stat-num[data-count]');
  const runCounter = (el) => {
    const target = parseInt(el.dataset.count, 10) || 0;
    if (reduce) { el.textContent = String(target); return; }
    const start = performance.now();
    const step = (now) => {
      const p = Math.min((now - start) / 1100, 1);
      el.textContent = String(Math.round(target * (1 - Math.pow(1 - p, 3))));
      if (p < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  };
  if ('IntersectionObserver' in window) {
    const countIO = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) { runCounter(entry.target); countIO.unobserve(entry.target); }
      });
    }, { threshold: 0.6 });
    counters.forEach((el) => countIO.observe(el));
  } else {
    counters.forEach(runCounter);
  }

  /* ---------- interacción puntero ---------- */
  const finePointer = window.matchMedia('(hover:hover) and (pointer:fine)').matches;
  const glow = $('#cursor-glow');
  if (glow && finePointer && !reduce) {
    window.addEventListener('mousemove', (e) => {
      glow.style.opacity = '1';
      glow.style.left = `${e.clientX}px`;
      glow.style.top = `${e.clientY}px`;
    }, { passive: true });
    document.addEventListener('mouseleave', () => { glow.style.opacity = '0'; });
  }

  if (finePointer && !reduce) {
    $$('.magnetic').forEach((el) => {
      el.addEventListener('mousemove', (e) => {
        const r = el.getBoundingClientRect();
        el.style.setProperty('--mx', `${((e.clientX - r.left - r.width / 2) * 0.14).toFixed(2)}px`);
        el.style.setProperty('--my', `${((e.clientY - r.top - r.height / 2) * 0.14).toFixed(2)}px`);
      });
      el.addEventListener('mouseleave', () => {
        el.style.setProperty('--mx', '0px');
        el.style.setProperty('--my', '0px');
      });
    });

    $$('[data-tilt]').forEach((el) => {
      el.addEventListener('mousemove', (e) => {
        const r = el.getBoundingClientRect();
        const x = (e.clientX - r.left) / r.width - 0.5;
        const y = (e.clientY - r.top) / r.height - 0.5;
        el.style.setProperty('--rx', `${(-y * 5).toFixed(2)}deg`);
        el.style.setProperty('--ry', `${(x * 5).toFixed(2)}deg`);
        el.style.setProperty('--ty', '-3px');
      });
      el.addEventListener('mouseleave', () => {
        el.style.setProperty('--rx', '0deg');
        el.style.setProperty('--ry', '0deg');
        el.style.setProperty('--ty', '0px');
      });
    });

    $$('.card, .edition-card, .bento-card').forEach((el) => {
      el.addEventListener('mousemove', (e) => {
        const r = el.getBoundingClientRect();
        el.style.setProperty('--sx', `${((e.clientX - r.left) / r.width * 100).toFixed(1)}%`);
        el.style.setProperty('--sy', `${((e.clientY - r.top) / r.height * 100).toFixed(1)}%`);
      });
    });

    const parallaxEls = $$('[data-parallax]');
    window.addEventListener('mousemove', (e) => {
      const cx = window.innerWidth / 2;
      const cy = window.innerHeight / 2;
      const dx = (e.clientX - cx) / cx;
      const dy = (e.clientY - cy) / cy;
      parallaxEls.forEach((el) => {
        const amt = parseFloat(el.dataset.parallax || '12');
        el.style.setProperty('--px', `${(dx * amt).toFixed(2)}px`);
        el.style.setProperty('--py', `${(dy * amt).toFixed(2)}px`);
      });
    }, { passive: true });
  }

  /* ---------- partículas de fondo ---------- */
  const canvas = $('#bg-canvas');
  if (canvas && canvas.getContext) {
    const ctx = canvas.getContext('2d', { alpha: true });
    let w = 0, h = 0, particles = [], lastFrame = 0, running = true;
    const resize = () => {
      const dpr = Math.min(window.devicePixelRatio || 1, 1.5);
      w = window.innerWidth; h = window.innerHeight;
      canvas.width = Math.floor(w * dpr);
      canvas.height = Math.floor(h * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const count = Math.min(w < 720 ? 32 : 64, Math.floor((w * h) / 26000));
      particles = Array.from({ length: Math.max(12, count) }, () => ({
        x: Math.random() * w, y: Math.random() * h,
        r: Math.random() * 1.5 + 0.4,
        vx: (Math.random() - 0.5) * 0.24,
        vy: (Math.random() - 0.5) * 0.24,
        a: Math.random() * 0.34 + 0.12
      }));
    };
    const draw = (now) => {
      requestAnimationFrame(draw);
      if (!running || (!reduce && now - lastFrame < 32)) return;
      lastFrame = now;
      ctx.clearRect(0, 0, w, h);
      const g = ctx.createRadialGradient(w * 0.72, h * 0.2, 0, w * 0.72, h * 0.2, Math.max(w, h) * 0.72);
      g.addColorStop(0, 'rgba(96,217,160,0.055)');
      g.addColorStop(0.5, 'rgba(85,183,255,0.025)');
      g.addColorStop(1, 'rgba(7,11,17,0)');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
      for (let i = 0; i < particles.length; i++) {
        const p = particles[i];
        if (!reduce) {
          p.x += p.vx; p.y += p.vy;
          if (p.x < 0) p.x = w; else if (p.x > w) p.x = 0;
          if (p.y < 0) p.y = h; else if (p.y > h) p.y = 0;
        }
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(150,235,195,${p.a})`;
        ctx.fill();
        for (let j = i + 1; j < particles.length; j++) {
          const q = particles[j];
          const dx = p.x - q.x, dy = p.y - q.y;
          const d2 = dx * dx + dy * dy;
          if (d2 < 130 * 130) {
            ctx.strokeStyle = `rgba(96,217,160,${(1 - Math.sqrt(d2) / 130) * 0.1})`;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(q.x, q.y);
            ctx.stroke();
          }
        }
      }
      if (reduce) running = false;
    };
    window.addEventListener('resize', resize, { passive: true });
    document.addEventListener('visibilitychange', () => { running = !document.hidden; });
    resize();
    requestAnimationFrame(draw);
  }

  /* ---------- mockup interactivo ---------- */
  const stageData = {
    java: {
      tag: 'JAVA EDITION',
      title: 'Versiones oficiales, listas para jugar',
      desc: 'Descarga desde los manifiestos de Mojang y lanza tu perfil en segundos.'
    },
    bedrock: {
      tag: 'BEDROCK WINDOWS',
      title: 'Tu Minecraft de Store, verificado',
      desc: 'Paquete, firma y componentes comprobados antes de cada lanzamiento.'
    },
    mobile: {
      tag: 'MOBILE CONNECT',
      title: 'Tu móvil en pantalla grande',
      desc: 'Transmisión en vivo con ADB y scrcpy. Juega con teclado y ratón.'
    }
  };
  const editionTabs = $$('.edition-tab');
  const stageTag = $('[data-stage-tag]');
  const stageTitle = $('[data-stage-title]');
  const stageDesc = $('[data-stage-desc]');

  const selectEdition = (name) => {
    if (!stageData[name]) return;
    editionTabs.forEach((tab) => {
      const active = tab.dataset.edition === name;
      tab.classList.toggle('active', active);
      tab.setAttribute('aria-selected', String(active));
    });
    $$('.app-panel').forEach((panel) => panel.classList.toggle('active', panel.dataset.panel === name));
    if (stageTag) stageTag.textContent = stageData[name].tag;
    if (stageTitle) stageTitle.textContent = stageData[name].title;
    if (stageDesc) stageDesc.textContent = stageData[name].desc;
  };
  editionTabs.forEach((tab) => tab.addEventListener('click', () => selectEdition(tab.dataset.edition)));

  $$('.rail-btn').forEach((btn) => {
    btn.addEventListener('click', () => {
      $$('.rail-btn').forEach((b) => b.classList.toggle('active', b === btn));
      const rail = btn.dataset.rail;
      if (rail === 'bedrock' || rail === 'mobile') selectEdition(rail);
      else if (rail === 'java' || rail === 'home') selectEdition('java');
    });
  });

  const playTimers = [];
  const playDemo = (btn) => {
    if (!btn || btn.classList.contains('launching')) return;
    const label = $('[data-play-label]', btn) || $('span', btn);
    const original = label ? label.textContent : 'JUGAR';
    btn.classList.add('launching');
    if (label) label.textContent = 'PREPARANDO…';
    playTimers.push(setTimeout(() => { if (label) label.textContent = 'LISTO'; }, reduce ? 120 : 1200));
    playTimers.push(setTimeout(() => {
      btn.classList.remove('launching');
      if (label) label.textContent = original;
    }, reduce ? 420 : 2100));
  };
  const mockPlay = $('#mock-play');
  mockPlay && mockPlay.addEventListener('click', () => playDemo(mockPlay));
  $$('.play-demo').forEach((btn) => btn.addEventListener('click', () => playDemo(btn)));

  /* ---------- selector de versiones ---------- */
  const versionRows = $$('#version-list .vrow');
  versionRows.forEach((row) => row.addEventListener('click', () => {
    versionRows.forEach((r) => r.classList.toggle('active', r === row));
  }));

  /* ---------- FAQ ---------- */
  const faqItems = $$('.faq-item');
  faqItems.forEach((item) => item.addEventListener('toggle', () => {
    if (item.open) faqItems.forEach((other) => { if (other !== item) other.open = false; });
  }));

  /* ---------- noticias oficiales (fuente real) ---------- */
  const newsSide = $('#news-side');
  const fallbackNews = [
    { title: 'Consulta las novedades oficiales en Minecraft.net', url: 'https://www.minecraft.net/es-es/updates', category: 'Sitio oficial' },
    { title: 'Historial de versiones del launcher', url: 'https://github.com/lferreyra-star/MinecraftLauncher/releases', category: 'GitHub Releases' }
  ];
  const renderNews = (items) => {
    if (!newsSide) return;
    newsSide.textContent = '';
    (items.length ? items.slice(0, 3) : fallbackNews).forEach((item) => {
      const a = document.createElement('a');
      a.className = 'news-row';
      a.href = item.url;
      a.target = '_blank';
      a.rel = 'noopener';
      const icon = document.createElement('span');
      icon.className = 'halo-icon';
      icon.textContent = '📰';
      icon.setAttribute('aria-hidden', 'true');
      const text = document.createElement('span');
      const b = document.createElement('b');
      b.textContent = item.title;
      const small = document.createElement('small');
      small.textContent = item.category + (item.date ? ' · ' + item.date : '');
      text.append(b, small);
      a.append(icon, text);
      newsSide.append(a);
    });
  };
  renderNews([]);
  const fetchWithTimeout = (url, ms) => {
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), ms);
    return fetch(url, { mode: 'cors', signal: ctrl.signal }).finally(() => clearTimeout(timer));
  };
  fetchWithTimeout('https://launchermeta.mojang.com/mc/game/version_manifest_v2.json', 6000)
    .then((r) => (r.ok ? r.json() : null))
    .then((data) => {
      const version = data && data.latest && data.latest.release;
      if (!version) return;
      const info = (data.versions || []).find((v) => v.id === version);
      renderNews([{
        title: `Minecraft Java Edition ${version} ya disponible`,
        url: 'https://www.minecraft.net/es-es/updates',
        category: 'Release',
        date: info && info.time ? new Date(info.time).toLocaleDateString('es-ES') : ''
      }]);
    })
    .catch(() => { /* se conserva la lista de respaldo */ });

  /* ---------- diversos ---------- */
  const year = $('#footer-year');
  if (year) year.textContent = String(new Date().getFullYear());
})();
