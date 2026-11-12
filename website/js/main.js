import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-app.js";
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut, onAuthStateChanged, setPersistence, browserLocalPersistence, signInWithEmailAndPassword, createUserWithEmailAndPassword, sendPasswordResetEmail, updateProfile } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-auth.js";
import { getFirestore, doc, getDoc, setDoc, serverTimestamp } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-firestore.js";

(() => {
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const reduce = false;

  // nav
  const nav = $('#nav');
  const onScroll = () => nav && nav.classList.toggle('scrolled', window.scrollY > 20);
  window.addEventListener('scroll', onScroll, { passive: true }); onScroll();
  const toggle = $('#nav-toggle'), links = $('#nav-links');
  if (toggle && links) {
    toggle.addEventListener('click', () => links.classList.toggle('open'));
    $$('a', links).forEach(a => a.addEventListener('click', () => links.classList.remove('open')));
  }

  // reveal
  const revealEls = $$('.reveal');
  const showReveal = (el) => el.classList.add('visible');
  if ('IntersectionObserver' in window) {
    const io = new IntersectionObserver((entries) => entries.forEach(e => {
      if (e.isIntersecting) { showReveal(e.target); io.unobserve(e.target); }
    }), { threshold: 0.12 });
    revealEls.forEach(el => io.observe(el));
  } else { revealEls.forEach(showReveal); }
  const revealVisible = () => revealEls.forEach(el => { const r = el.getBoundingClientRect(); if (r.top < window.innerHeight) showReveal(el); });
  window.addEventListener('load', revealVisible);
  setTimeout(revealVisible, 150); setTimeout(() => revealEls.forEach(showReveal), 1800);

  // counters
  const countIO = ('IntersectionObserver' in window) ? new IntersectionObserver((entries) => entries.forEach(e => {
    if (!e.isIntersecting) return;
    const el = e.target, target = parseInt(el.dataset.count, 10) || 0, start = performance.now();
    const step = (now) => { const p = Math.min((now - start) / 1200, 1), eased = 1 - Math.pow(1 - p, 3); el.textContent = Math.round(target * eased); if (p < 1) requestAnimationFrame(step); };
    requestAnimationFrame(step); countIO.unobserve(el);
  }), { threshold: 0.6 }) : null;
  $$('.stat-num[data-count]').forEach(c => countIO ? countIO.observe(c) : (c.textContent = c.dataset.count));

  // cursor glow
  const glow = $('#cursor-glow');
  if (glow && !reduce) {
    window.addEventListener('mousemove', (e) => { glow.style.opacity = '1'; glow.style.left = e.clientX + 'px'; glow.style.top = e.clientY + 'px'; }, { passive: true });
    window.addEventListener('mouseleave', () => glow.style.opacity = '0');
  }

  // parallax + tilt + magnetic
  if (!reduce) {
    const parEls = $$('[data-parallax]');
    window.addEventListener('mousemove', (e) => {
      const cx = window.innerWidth / 2, cy = window.innerHeight / 2, dx = (e.clientX - cx) / cx, dy = (e.clientY - cy) / cy;
      parEls.forEach(el => { const amt = parseFloat(el.dataset.parallax || '10'); el.style.transform = `translate(${dx * amt}px, ${dy * amt}px)`; });
    }, { passive: true });
  }
  $$('.tilt').forEach(card => {
    if (reduce) return;
    card.addEventListener('mousemove', (e) => { const r = card.getBoundingClientRect(); const x = (e.clientX - r.left) / r.width - 0.5; const y = (e.clientY - r.top) / r.height - 0.5; card.style.transform = `perspective(900px) rotateX(${(-y * 6).toFixed(2)}deg) rotateY(${(x * 6).toFixed(2)}deg) translateY(-4px)`; });
    card.addEventListener('mouseleave', () => { card.style.transform = ''; });
  });
  $$('.magnetic').forEach(el => {
    if (reduce) return;
    el.addEventListener('mousemove', (e) => { const r = el.getBoundingClientRect(); el.style.transform = `translate(${(e.clientX - (r.left + r.width / 2)) * 0.18}px, ${(e.clientY - (r.top + r.height / 2)) * 0.18}px)`; });
    el.addEventListener('mouseleave', () => { el.style.transform = ''; });
  });

  // particles
  const canvas = $('#bg-canvas');
  if (canvas) {
    const ctx = canvas.getContext('2d');
    let w, h, particles;
    function resize() { w = canvas.width = window.innerWidth; h = canvas.height = window.innerHeight; const count = Math.min(90, Math.floor((w * h) / 22000)); particles = Array.from({ length: count }, () => ({ x: Math.random() * w, y: Math.random() * h, r: Math.random() * 1.8 + 0.4, vx: (Math.random() - 0.5) * 0.3, vy: (Math.random() - 0.5) * 0.3, a: Math.random() * 0.5 + 0.15 })); }
    function draw() {
      ctx.clearRect(0, 0, w, h);
      const g = ctx.createRadialGradient(w * 0.7, h * 0.2, 0, w * 0.7, h * 0.2, Math.max(w, h) * 0.7);
      g.addColorStop(0, 'rgba(92,224,122,0.07)'); g.addColorStop(0.5, 'rgba(53,208,255,0.03)'); g.addColorStop(1, 'rgba(11,15,20,0)');
      ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
      for (let i = 0; i < particles.length; i++) {
        const p = particles[i];
        if (!reduce) { p.x += p.vx; p.y += p.vy; if (p.x < 0) p.x = w; if (p.x > w) p.x = 0; if (p.y < 0) p.y = h; if (p.y > h) p.y = 0; }
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fillStyle = `rgba(156,242,172,${p.a})`; ctx.fill();
        for (let j = i + 1; j < particles.length; j++) { const q = particles[j], dx = p.x - q.x, dy = p.y - q.y, d = dx * dx + dy * dy; if (d < 150 * 150) { ctx.strokeStyle = `rgba(92,224,122,${(1 - Math.sqrt(d) / 150) * 0.12})`; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(q.x, q.y); ctx.stroke(); } }
      }
      if (!reduce) requestAnimationFrame(draw);
    }
    window.addEventListener('resize', resize); resize(); draw();
  }

  // ===== Firebase Auth + Firestore (Google) =====
  const modal = $('#auth-modal'), openLogin = $('#open-login'), closeLogin = $('#auth-close'), googleBtn = $('#google-login-btn');
  const msg = $('#auth-msg');
  const userMenu = $('#user-menu'), userChip = $('#user-chip');
  const setMsg = (t, type = '') => { if (!msg) return; msg.textContent = t || ''; msg.className = 'auth-msg ' + type; };
  const openModal = () => { modal && modal.classList.add('open'); modal && modal.setAttribute('aria-hidden', 'false'); setMsg(''); };
  const closeModal = () => { modal && modal.classList.remove('open'); modal && modal.setAttribute('aria-hidden', 'true'); };
  openLogin && openLogin.addEventListener('click', openModal);
  closeLogin && closeLogin.addEventListener('click', closeModal);
  modal && modal.addEventListener('click', (e) => { if (e.target === modal) closeModal(); });
  window.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeModal(); });
  userChip && userChip.addEventListener('click', () => userMenu.classList.toggle('open'));
  document.addEventListener('click', (e) => { if (userMenu && !userMenu.contains(e.target)) userMenu.classList.remove('open'); });

  function fmtTs(ts) {
    if (!ts) return '—';
    try { const d = ts.toDate ? ts.toDate() : new Date(ts); return d.toLocaleString(); } catch { return '—'; }
  }

  function renderUser(user, profile) {
    const logged = !!user;
    if (openLogin) openLogin.hidden = logged;
    if (userMenu) userMenu.hidden = !logged;
    const profileSection = $('#profile');
    if (profileSection) profileSection.hidden = !logged;

    const name = (user && (user.displayName || 'Usuario')) || '';
    const email = (user && user.email) || '';
    const photo = (user && user.photoURL) || 'assets/logo.png';

    const setImg = (id, src) => { const el = document.getElementById(id); if (el) { el.src = src; el.referrerPolicy = 'no-referrer'; } };
    const setTxt = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val || '—'; };

    setTxt('user-name', name); setTxt('user-email', email); setImg('user-avatar', photo);
    setTxt('user-name-lg', name); setImg('user-avatar-lg', photo);
    setTxt('profile-name', name); setTxt('profile-email', email); setImg('profile-avatar', photo);
    setTxt('profile-provider', 'Google');
    setTxt('profile-uid', user ? `UID: ${user.uid}` : '');
    setTxt('profile-created', profile ? fmtTs(profile.createdAt) : '—');
    setTxt('profile-last', profile ? fmtTs(profile.lastLoginAt) : '—');
    if (logged) closeModal();
  }

  const cfg = window.FIREBASE_CONFIG || {};
  const configured = cfg.apiKey && !String(cfg.apiKey).startsWith('TU_') && cfg.projectId && !String(cfg.projectId).startsWith('TU_');

  if (!configured) {
    if (googleBtn) googleBtn.addEventListener('click', () => { openModal(); setMsg('Configura tu Firebase en js/config.js para habilitar el inicio de sesión.', 'error'); });
    renderUser(null, null);
    return;
  }

  let app, auth, db;
  try {
    app = initializeApp(cfg);
    auth = getAuth(app);
    db = getFirestore(app);
    setPersistence(auth, browserLocalPersistence).catch(() => {});
  } catch (e) {
    if (googleBtn) googleBtn.addEventListener('click', () => { openModal(); setMsg('Error al inicializar Firebase. Revisa js/config.js.', 'error'); });
    renderUser(null, null);
    return;
  }

  async function ensureUserProfile(user) {
    // Construye perfil SOLO desde el usuario autenticado (no datos arbitrarios del cliente)
    const ref = doc(db, 'users', user.uid);
    const snap = await getDoc(ref);
    const provider = user.providerData && user.providerData.length ? user.providerData[0].providerId : 'google.com';
    const base = {
      uid: user.uid,
      name: user.displayName || '',
      email: user.email || '',
      photoURL: user.photoURL || null,
      provider: provider === 'password' ? 'password' : 'google.com'
    };
    if (!snap.exists()) {
      await setDoc(ref, { ...base, createdAt: serverTimestamp(), lastLoginAt: serverTimestamp() });
    } else {
      // Solo campos permitidos por reglas (no toca uid/provider/createdAt)
      await setDoc(ref, { name: base.name, email: base.email, photoURL: base.photoURL, lastLoginAt: serverTimestamp() }, { merge: true });
    }
    const fresh = await getDoc(ref);
    return fresh.exists() ? fresh.data() : null;
  }

  // ===== Login por correo y contraseña (misma cuenta que el launcher de escritorio) =====
  const tabLogin = $('#tab-login'), tabRegister = $('#tab-register');
  const formLogin = $('#auth-form-login'), formRegister = $('#auth-form-register');
  const loginEmail = $('#login-email'), loginPassword = $('#login-password');
  const regName = $('#reg-name'), regEmail = $('#reg-email'), regPassword = $('#reg-password');
  const loginSubmit = $('#login-submit'), registerSubmit = $('#register-submit'), resetBtn = $('#reset-password-btn');

  const showTab = (register) => {
    if (formLogin) formLogin.classList.toggle('hidden', register);
    if (formRegister) formRegister.classList.toggle('hidden', !register);
    if (tabLogin) { tabLogin.classList.toggle('active', !register); tabLogin.setAttribute('aria-selected', String(!register)); }
    if (tabRegister) { tabRegister.classList.toggle('active', register); tabRegister.setAttribute('aria-selected', String(register)); }
    setMsg('');
  };
  tabLogin && tabLogin.addEventListener('click', () => showTab(false));
  tabRegister && tabRegister.addEventListener('click', () => showTab(true));

  const bindEye = (input, eye) => {
    eye && eye.addEventListener('click', () => {
      if (!input) return;
      input.type = input.type === 'password' ? 'text' : 'password';
      eye.textContent = input.type === 'password' ? '👁' : '🙈';
    });
  };
  bindEye(loginPassword, $('#login-eye'));
  bindEye(regPassword, $('#reg-eye'));

  const authError = (err, prefix) => {
    const code = err && err.code ? err.code : '';
    if (code === 'auth/invalid-credential' || code === 'auth/wrong-password' || code === 'auth/user-not-found')
      return 'El correo o la contraseña no son correctos.';
    if (code === 'auth/invalid-email') return 'El correo no tiene un formato válido.';
    if (code === 'auth/email-already-in-use') return 'Ese correo ya está registrado.';
    if (code === 'auth/weak-password') return 'La contraseña es demasiado débil (mínimo 6 caracteres).';
    if (code === 'auth/too-many-requests') return 'Demasiados intentos. Espera unos minutos.';
    if (code === 'auth/network-request-failed') return 'Sin conexión con el servidor de autenticación.';
    if (code === 'auth/operation-not-allowed') return 'Habilita el proveedor Correo/contraseña en Firebase Authentication.';
    if (code === 'auth/unauthorized-domain') return 'Dominio no autorizado. Agrégalo en Firebase > Authentication > Settings > Authorized domains.';
    return `${prefix} ${err && err.message ? err.message : ''}`.trim();
  };

  formLogin && formLogin.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!loginEmail || !loginPassword) return;
    if (loginSubmit) { loginSubmit.disabled = true; loginSubmit.textContent = 'Comprobando…'; }
    setMsg('Comprobando credenciales…');
    try {
      await signInWithEmailAndPassword(auth, loginEmail.value.trim(), loginPassword.value);
      setMsg('Sesión iniciada.', 'ok');
    } catch (err) {
      setMsg(authError(err, 'No se pudo iniciar sesión:'), 'error');
    } finally {
      if (loginSubmit) { loginSubmit.disabled = false; loginSubmit.textContent = 'Iniciar sesión'; }
    }
  });

  formRegister && formRegister.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!regEmail || !regPassword) return;
    if (registerSubmit) { registerSubmit.disabled = true; registerSubmit.textContent = 'Creando cuenta…'; }
    setMsg('Creando tu cuenta…');
    try {
      const cred = await createUserWithEmailAndPassword(auth, regEmail.value.trim(), regPassword.value);
      if (cred && cred.user && regName && regName.value.trim()) {
        await updateProfile(cred.user, { displayName: regName.value.trim() });
      }
      setMsg('Cuenta creada. Sesión iniciada.', 'ok');
    } catch (err) {
      setMsg(authError(err, 'No se pudo crear la cuenta:'), 'error');
    } finally {
      if (registerSubmit) { registerSubmit.disabled = false; registerSubmit.textContent = 'Crear cuenta'; }
    }
  });

  resetBtn && resetBtn.addEventListener('click', async () => {
    const email = loginEmail && loginEmail.value.trim();
    if (!email) { setMsg('Escribe tu correo para enviarte el enlace de recuperación.', 'error'); return; }
    setMsg('Enviando correo de recuperación…');
    try {
      await sendPasswordResetEmail(auth, email);
      setMsg('Te enviamos un correo con el enlace para crear una contraseña nueva.', 'ok');
    } catch (err) {
      setMsg(authError(err, 'No se pudo enviar el correo:'), 'error');
    }
  });

  googleBtn && googleBtn.addEventListener('click', async () => {
    setMsg('Conectando con Google…');
    googleBtn.disabled = true;
    try {
      const provider = new GoogleAuthProvider();
      provider.setCustomParameters({ prompt: 'select_account' });
      const result = await signInWithPopup(auth, provider);
      // nuevo o existente: onAuthStateChanged se encargará de reflejar y crear/actualizar perfil
      if (result && result.user) setMsg('Sesión iniciada.', 'ok');
    } catch (err) {
      const code = err && err.code ? err.code : '';
      if (code === 'auth/popup-closed-by-user' || code === 'auth/cancelled-popup-request') setMsg('Inicio de sesión cancelado.');
      else if (code === 'auth/unauthorized-domain') setMsg('Dominio no autorizado. Agrégalo en Firebase > Authentication > Settings > Authorized domains.', 'error');
      else if (code === 'auth/operation-not-allowed') setMsg('Habilita el proveedor Google en Firebase Authentication.', 'error');
      else setMsg('No se pudo iniciar sesión con Google. Revisa la configuración de Firebase.', 'error');
    } finally {
      googleBtn.disabled = false;
    }
  });

  // ===== Traspaso de sesión al launcher de escritorio (xdlauncher://) =====
  const DESKTOP_SCHEME = 'xdlauncher://auth';
  const isDesktopFlow = new URLSearchParams(window.location.search).get('desktop') === '1';

  async function handoffToDesktop(user) {
    if (!user) return;
    try {
      const idToken = await user.getIdToken();
      const payload = new URLSearchParams({ token: idToken });
      window.location.href = `${DESKTOP_SCHEME}?${payload.toString()}`;
      setMsg('Sesión enviada al launcher. Vuelve a la aplicación de escritorio.', 'ok');
    } catch (e) {
      setMsg('No se pudo enviar la sesión al launcher.', 'error');
    }
  }

  const desktopBtn = $('#desktop-handoff-btn');
  desktopBtn && desktopBtn.addEventListener('click', () => {
    if (auth && auth.currentUser) handoffToDesktop(auth.currentUser);
    else setMsg('Inicia sesión primero para conectar el launcher de escritorio.', 'error');
  });

  if (isDesktopFlow) {
    onAuthStateChanged(auth, async (user) => {
      if (!user) { setMsg('Inicia sesión en esta pestaña para continuar en el launcher.', 'error'); return; }
      await handoffToDesktop(user);
    });
  }

  async function doLogout() { try { await signOut(auth); } catch {} }
  $('#logout-btn') && $('#logout-btn').addEventListener('click', doLogout);
  $('#profile-logout') && $('#profile-logout').addEventListener('click', doLogout);

  onAuthStateChanged(auth, async (user) => {
    if (!user) { renderUser(null, null); return; }
    // usuario autenticado (nuevo o existente). Mostrar datos y sincronizar perfil propio en Firestore.
    renderUser(user, null);
    try {
      const profile = await ensureUserProfile(user);
      renderUser(user, profile);
    } catch {
      // si Firestore falla (reglas/config), mantenemos la sesión visible con datos de Auth
      renderUser(user, null);
    }
  });
})();
