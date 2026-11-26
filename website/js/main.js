import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-app.js";
import { getAuth, GoogleAuthProvider, signInWithPopup, signInWithRedirect, getRedirectResult, signOut, onAuthStateChanged, setPersistence, browserLocalPersistence, signInWithEmailAndPassword, createUserWithEmailAndPassword, sendPasswordResetEmail, updateProfile } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-auth.js";
import { getFirestore, doc, getDoc, setDoc, serverTimestamp } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-firestore.js";

(() => {
  // Marcador de versión: permite verificar desde fuera qué build carga el navegador.
  document.documentElement.setAttribute('data-xd-auth', 'xd-1.0');
  const $ = (s, r = document) => r.querySelector(s);

  // ===== Firebase Auth + Firestore (Google y correo/contraseña) =====
  const modal = $('#auth-modal'), openLogin = $('#open-login'), closeLogin = $('#auth-close'), googleBtn = $('#google-login-btn');
  const msg = $('#auth-msg');
  const userMenu = $('#user-menu'), userChip = $('#user-chip');
  const setMsg = (t, type = '') => { if (!msg) return; msg.textContent = t || ''; msg.className = 'auth-msg ' + type; };
  const openModal = () => {
    if (!modal) return;
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    document.body.classList.add('modal-open');
    setMsg('');
  };
  const closeModal = () => {
    if (!modal) return;
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    document.body.classList.remove('modal-open');
  };
  openLogin && openLogin.addEventListener('click', openModal);
  closeLogin && closeLogin.addEventListener('click', closeModal);
  modal && modal.addEventListener('click', (e) => { if (e.target === modal) closeModal(); });
  window.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeModal(); });
  userChip && userChip.addEventListener('click', () => {
    const open = userMenu.classList.toggle('open');
    userChip.setAttribute('aria-expanded', String(open));
  });
  document.addEventListener('click', (e) => {
    if (userMenu && !userMenu.contains(e.target)) {
      userMenu.classList.remove('open');
      userChip && userChip.setAttribute('aria-expanded', 'false');
    }
  });

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
      const code = err && err.code ? err.code : '';
      if (code === 'auth/operation-not-allowed' || code === 'auth/password-login-disabled') {
        setMsg('El acceso con contraseña está deshabilitado en el servidor. Usa «Continuar con Google» para entrar ahora mismo.', 'error');
        if (formLogin) formLogin.classList.add('hidden');
        if (formRegister) formRegister.classList.add('hidden');
        if (tabLogin) tabLogin.classList.remove('active');
        if (tabRegister) tabRegister.classList.remove('active');
      } else {
        setMsg(authError(err, 'No se pudo iniciar sesión:'), 'error');
      }
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
      const code = err && err.code ? err.code : '';
      if (code === 'auth/operation-not-allowed' || code === 'auth/password-login-disabled') {
        setMsg('El registro con contraseña está deshabilitado en el servidor. Te llevamos a Google, que crea la misma cuenta.', 'ok');
        await startGoogle();
      } else {
        setMsg(authError(err, 'No se pudo crear la cuenta:'), 'error');
      }
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

  // Google: se intenta el popup (mantiene la página sin recargar). Solo se cae a la
  // redirección completa cuando el navegador bloquea la ventana emergente.
  const canFallbackToRedirect = (code) =>
    code === 'auth/popup-blocked' || code === 'auth/operation-not-supported-in-this-environment';

  const startGoogle = async () => {
    const provider = new GoogleAuthProvider();
    provider.setCustomParameters({ prompt: 'select_account' });
    try {
      const result = await signInWithPopup(auth, provider);
      if (result && result.user) return result.user;
    } catch (err) {
      const code = err && err.code ? err.code : '';
      if (canFallbackToRedirect(code)) {
        await signInWithRedirect(auth, provider);
        return null;
      }
      throw err;
    }
    return null;
  };

  googleBtn && googleBtn.addEventListener('click', async () => {
    setMsg('Conectando con Google…');
    googleBtn.disabled = true;
    try {
      await startGoogle();
    } catch (err) {
      const code = err && err.code ? err.code : '';
      if (code === 'auth/unauthorized-domain') { setMsg('Dominio no autorizado. Agrégalo en Firebase > Authentication > Settings > Authorized domains.', 'error'); return; }
      if (code === 'auth/operation-not-allowed') { setMsg('Habilita el proveedor Google en Firebase Authentication.', 'error'); return; }
      if (code === 'auth/popup-closed-by-user' || code === 'auth/cancelled-popup-request') { setMsg('Inicio de sesión cancelado.'); return; }
      setMsg(authError(err, 'No se pudo iniciar sesión con Google:'), 'error');
    } finally {
      googleBtn.disabled = false;
    }
  });

  // ===== Traspaso de sesión al launcher de escritorio (xdlauncher://) =====
  const DESKTOP_SCHEME = 'xdlauncher://auth';
  const isDesktopFlow = new URLSearchParams(window.location.search).get('desktop') === '1';
  let handoffSent = false;

  async function handoffToDesktop(user, force = false) {
    if (!user || (handoffSent && !force)) return;
    handoffSent = true;
    try {
      const idToken = await user.getIdToken(true);
      if (!idToken) throw new Error('Firebase no devolvió un token de sesión.');
      const params = new URLSearchParams({ token: idToken });
      if (user.refreshToken) params.set('refresh', user.refreshToken);
      setMsg('Enviando la sesión al launcher… Vuelve a la aplicación de escritorio.', 'ok');
      window.location.href = `${DESKTOP_SCHEME}?${params.toString()}`;
    } catch (e) {
      handoffSent = false;
      setMsg('No se pudo enviar la sesión al launcher. Vuelve al launcher e inténtalo de nuevo.', 'error');
    }
  }

  const desktopBtn = $('#desktop-handoff-btn');
  desktopBtn && desktopBtn.addEventListener('click', () => {
    if (auth && auth.currentUser) handoffToDesktop(auth.currentUser, true);
    else setMsg('Inicia sesión primero para conectar el launcher de escritorio.', 'error');
  });

  if (isDesktopFlow) {
    setMsg('Inicia sesión con Google para enviar la sesión al launcher.');
  }

  async function doLogout() { try { await signOut(auth); } catch {} }
  $('#logout-btn') && $('#logout-btn').addEventListener('click', doLogout);
  $('#profile-logout') && $('#profile-logout').addEventListener('click', doLogout);

  // Solo se consulta si la URL trae parámetros de redirección: pedirlo sin
  // necesidad deja la promesa colgada y ralentiza la carga en producción.
  const hasRedirectParams = () => {
    const search = new URLSearchParams(window.location.search);
    const hash = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    return search.has('apiKey') || search.has('eventId') || hash.has('apiKey') || hash.has('eventId');
  };

  if (hasRedirectParams()) {
    const redirectTimeout = new Promise((resolve) => setTimeout(() => resolve(null), 15000));
    Promise.race([getRedirectResult(auth).catch((err) => { throw err; }), redirectTimeout])
      .then((redirectResult) => {
        if (redirectResult && redirectResult.user) setMsg('Sesión iniciada.', 'ok');
      })
      .catch((err) => {
        const code = err && err.code ? err.code : '';
        if (code === 'auth/user-cancelled' || code === 'auth/cancelled-popup-request') return;
        setMsg(authError(err, 'No se pudo completar el acceso:'), 'error');
      });
  }

  onAuthStateChanged(auth, async (user) => {
    if (!user) { renderUser(null, null); return; }
    renderUser(user, null);
    setMsg('Sesión iniciada.', 'ok');
    try {
      const profile = await ensureUserProfile(user);
      renderUser(user, profile);
    } catch {
      // si Firestore falla (reglas/config), mantenemos la sesión visible con datos de Auth
      renderUser(user, null);
    }
    if (isDesktopFlow) await handoffToDesktop(user);
  });
})();
