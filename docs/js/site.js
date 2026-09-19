// NEXUS - interacciones: GSAP reveals (sin fondo 3D, tema plano)
(function () {
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---- 1. GSAP reveals + contadores + tilt ----
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
    document.querySelectorAll('.navbar-neon .nav-link, .nx-side .nav-link').forEach(a => {
      var href = (a.getAttribute('href') || '').toLowerCase();
      if (href === '/' && (path === '/' || path.includes('index'))) a.classList.add('active');
      else if (href !== '/' && href.startsWith('http') === false && path.startsWith(href)) a.classList.add('active');
    });
  } catch { /* noop */ }
})();
