// INNOVATE STORE - Neon interactions: Three.js starfield + GSAP reveals
(function () {
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---- 1. Fondo 3D partículas (Three.js) ----
  try {
    const canvas = document.getElementById('bg-3d');
    if (canvas && window.THREE && !reduceMotion) {
      const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
      const scene = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 100);
      camera.position.z = 8;

      const count = window.innerWidth < 640 ? 500 : 1200;
      const geo = new THREE.BufferGeometry();
      const pos = new Float32Array(count * 3);
      const col = new Float32Array(count * 3);
      const cCyan = new THREE.Color(0x00f0ff), cMag = new THREE.Color(0xff2a6d), cGreen = new THREE.Color(0x05ffa1);
      for (let i = 0; i < count; i++) {
        pos[i * 3] = (Math.random() - 0.5) * 30;
        pos[i * 3 + 1] = (Math.random() - 0.5) * 18;
        pos[i * 3 + 2] = (Math.random() - 0.5) * 16;
        const r = Math.random();
        const c = r < 0.6 ? cCyan : r < 0.85 ? cMag : cGreen;
        col[i * 3] = c.r; col[i * 3 + 1] = c.g; col[i * 3 + 2] = c.b;
      }
      geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
      geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
      const mat = new THREE.PointsMaterial({ size: 0.045, vertexColors: true, transparent: true, opacity: 0.85 });
      const stars = new THREE.Points(geo, mat);
      scene.add(stars);

      let mx = 0, my = 0;
      window.addEventListener('mousemove', e => {
        mx = (e.clientX / window.innerWidth - 0.5) * 0.6;
        my = (e.clientY / window.innerHeight - 0.5) * 0.4;
      });
      function resize() {
        renderer.setSize(window.innerWidth, window.innerHeight, false);
        camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();
      }
      window.addEventListener('resize', resize); resize();
      (function tick(t) {
        stars.rotation.y = t * 0.00004 + mx;
        stars.rotation.x = my;
        renderer.render(scene, camera);
        requestAnimationFrame(tick);
      })(0);
    }
  } catch (e) { console.warn('3D bg off:', e); }

  // ---- 2. GSAP reveals + contadores + tilt ----
  function fallbackReveal() {
    document.querySelectorAll('.reveal').forEach(el => {
      const r = el.getBoundingClientRect();
      if (r.top < window.innerHeight * 0.9) el.classList.add('visible');
    });
  }

  try {
    if (window.gsap && !reduceMotion) {
      if (window.ScrollTrigger) gsap.registerPlugin(ScrollTrigger);
      gsap.from('#hero-title', { y: 40, opacity: 0, duration: 0.9, ease: 'power3.out' });
      gsap.from('#hero-card', { y: 30, opacity: 0, scale: 0.96, duration: 0.9, delay: 0.2, ease: 'back.out(1.4)' });

      const reveals = gsap.utils.toArray('.reveal');
      if (window.ScrollTrigger) {
        reveals.forEach(el => {
          gsap.fromTo(el, { y: 26, opacity: 0 }, {
            y: 0, opacity: 1, duration: 0.7, ease: 'power2.out',
            scrollTrigger: { trigger: el, start: 'top 88%' }
          });
        });
      } else {
        reveals.forEach(el => el.classList.add('visible'));
      }

      // Contadores hero
      document.querySelectorAll('[data-count]').forEach(el => {
        const target = parseInt(el.dataset.count, 10) || 0;
        const obj = { v: 0 };
        gsap.to(obj, {
          v: target, duration: 1.4, ease: 'power1.out',
          onUpdate: () => { el.textContent = Math.floor(obj.v); },
          scrollTrigger: window.ScrollTrigger ? { trigger: el, start: 'top 95%' } : undefined
        });
      });

      // Tilt en cards
      if (window.matchMedia('(pointer:fine)').matches) {
        document.querySelectorAll('.neon-card').forEach(card => {
          card.addEventListener('mousemove', e => {
            const r = card.getBoundingClientRect();
            const x = (e.clientX - r.left) / r.width - 0.5;
            const y = (e.clientY - r.top) / r.height - 0.5;
            gsap.to(card, { rotateY: x * 8, rotateX: -y * 8, transformPerspective: 700, duration: 0.3 });
          });
          card.addEventListener('mouseleave', () => gsap.to(card, { rotateX: 0, rotateY: 0, duration: 0.4 }));
        });
      }
    } else {
      fallbackReveal();
      window.addEventListener('scroll', fallbackReveal, { passive: true });
    }
  } catch (e) { console.warn('anim off:', e); fallbackReveal(); }

  // Navbar activa según ruta
  try {
    const path = window.location.pathname.toLowerCase();
    document.querySelectorAll('.navbar-neon .nav-link').forEach(a => {
      var href = (a.getAttribute('href') || '').toLowerCase();
      if (href === '/' && (path === '/' || path.includes('index'))) a.classList.add('active');
      else if (href !== '/' && href.startsWith('http') === false && path.startsWith(href)) a.classList.add('active');
    });
  } catch { /* noop */ }
})();
