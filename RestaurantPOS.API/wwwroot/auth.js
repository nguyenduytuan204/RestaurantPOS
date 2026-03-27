// ============================================================
//  auth.js  —  Module xác thực dùng chung cho mọi trang
//
//  CÁCH DÙNG: thêm vào đầu mỗi trang HTML cần bảo vệ:
//
//    <script src="auth.js"></script>
//    <script>
//      // Chỉ cho phép Admin và Quản lý vào trang admin
//      Auth.require([ROLES.ADMIN, ROLES.MANAGER]);
//    </script>
// ============================================================

const ROLES = {
    CASHIER: 0,   // Thu ngân
    WAITER:  1,   // Phục vụ
    MANAGER: 2,   // Quản lý
    ADMIN:   3,   // Admin
};

const ROLE_LABELS = {
    0: 'Thu ngân',
    1: 'Phục vụ',
    2: 'Quản lý',
    3: 'Admin',
};

const Auth = (() => {
    const TOKEN_KEY   = 'pos_token';
    const USER_KEY    = 'pos_user';
    const RECENT_USERS_KEY = 'pos_recent_users';

    // ── Lưu / lấy thông tin sau khi login ─────────────────
    function trackRecentUser(username) {
        let list = getRecentUsers();
        if (!list.includes(username)) {
            list.unshift(username);
            if (list.length > 5) list.pop();
            localStorage.setItem(RECENT_USERS_KEY, JSON.stringify(list));
        }
    }

    function getRecentUsers() {
        const raw = localStorage.getItem(RECENT_USERS_KEY);
        return raw ? JSON.parse(raw) : [];
    }

    function saveSession(data) {
        // data: { token, username, fullName, role, roleLabel, expiresAt, userId }
        trackRecentUser(data.username);
        localStorage.setItem(TOKEN_KEY, data.token);
        localStorage.setItem('pos_username',   data.username);
        localStorage.setItem('pos_fullname',   data.fullName);
        localStorage.setItem('pos_role',       data.role);
        localStorage.setItem('pos_rolelabel',  data.roleLabel);
        localStorage.setItem('pos_expires',    data.expiresAt);
        if (data.userId) localStorage.setItem('pos_userid', data.userId);
    }

    function getSession() {
        return {
            token:     localStorage.getItem(TOKEN_KEY),
            username:  localStorage.getItem('pos_username'),
            fullName:  localStorage.getItem('pos_fullname'),
            role:      parseInt(localStorage.getItem('pos_role')),
            roleLabel: localStorage.getItem('pos_rolelabel'),
            expiresAt: localStorage.getItem('pos_expires'),
            userId:    parseInt(localStorage.getItem('pos_userid')) || null // Changed default to null for consistency
        };
    }

    function getToken()  { return getSession().token; }
    function getUser()   {
        const session = getSession();
        // Reconstruct the user object as it was before, but with userId
        if (!session.username) return null; // If no username, assume no user data
        return {
            username:  session.username,
            fullName:  session.fullName,
            role:      session.role,
            roleLabel: session.roleLabel,
            expiresAt: session.expiresAt,
            userId:    session.userId,
        };
    }

    function isLoggedIn() {
        const session = getSession();
        if (!session.token || !session.username) return false;

        // Kiểm tra token chưa hết hạn (so sánh local)
        if (new Date(user.expiresAt) < new Date()) {
            logout(false); // hết hạn → xóa session
            return false;
        }
        return true;
    }

    function hasRole(allowedRoles) {
        const user = getUser();
        if (!user) return false;
        return allowedRoles.includes(user.role);
    }

    function logout(redirect = true) {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
        if (redirect) window.location.href = 'login';
    }

    // ── Bảo vệ trang — gọi ngay khi load ─────────────────
    // allowedRoles: mảng role được phép vào, VD [ROLES.ADMIN, ROLES.MANAGER]
    // Nếu không truyền → chỉ cần đăng nhập (bất kỳ role)
    function require(allowedRoles = null) {
        if (!isLoggedIn()) {
            window.location.href = 'login';
            return;
        }
        if (allowedRoles && !hasRole(allowedRoles)) {
            // Không đủ quyền → về trang phù hợp với role
            alert('Bạn không có quyền truy cập trang này.');
            redirectByRole();
        }
    }

    function redirectByRole() {
        const user = getUser();
        if (!user) { window.location.href = 'login'; return; }
        if (user.role >= ROLES.MANAGER) {
            window.location.href = 'admin_menu';
        } else if (user.role === ROLES.WAITER) {
            window.location.href = 'mobile_order';
        } else {
            window.location.href = 'pos_frontend';
        }
    }

    // ── Tạo Authorization header cho fetch ─────────────────
    function authHeaders(extra = {}) {
        return {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${getToken()}`,
            ...extra
        };
    }

    // ── Wrapper fetch tự thêm token + xử lý 401 ───────────
    async function apiFetch(url, options = {}) {
        const res = await fetch(url, {
            ...options,
            headers: authHeaders(options.headers),
        });
        if (res.status === 401) {
            logout(); // token hết hạn hoặc không hợp lệ
            return null;
        }
        return res;
    }

    // ── Render user info lên topbar ────────────────────────
    function renderUserBadge(nameEl, roleEl) {
        const user = getUser();
        if (!user) return;
        if (nameEl) nameEl.textContent = user.fullName;
        if (roleEl) roleEl.textContent = ROLE_LABELS[user.role] || '';
    }

    // ── Ẩn/hiện UI theo role ──────────────────────────────
    // Dùng attribute data-min-role="2" trên HTML element
    // Các element có role yêu cầu cao hơn role hiện tại sẽ bị ẩn
    function applyRoleVisibility() {
        const user = getUser();
        if (!user) return;
        document.querySelectorAll('[data-min-role]').forEach(el => {
            const minRole = parseInt(el.dataset.minRole);
            el.style.display = user.role >= minRole ? '' : 'none';
        });
    }

    // ── Tự động đăng xuất sau 30 phút không hoạt động ─────
    let inactivityTimer = null;
    const TIMEOUT_MS = 30 * 60 * 1000; // 30 phút

    function resetTimer() {
        if (!isLoggedIn()) return;
        clearTimeout(inactivityTimer);
        inactivityTimer = setTimeout(() => {
            alert('Phiên đăng nhập đã hết hạn do không hoạt động (30 phút). Vui lòng đăng nhập lại.');
            logout(true);
        }, TIMEOUT_MS);
    }

    if (window.location.pathname.indexOf('login') === -1 && isLoggedIn()) {
        ['mousemove', 'keydown', 'scroll', 'click', 'touchstart'].forEach(evt => {
            window.addEventListener(evt, resetTimer, { passive: true });
        });
        resetTimer();
    }

    return {
        saveSession,
        getToken,
        getUser,
        isLoggedIn,
        hasRole,
        logout,
        require,
        redirectByRole,
        authHeaders,
        apiFetch,
        renderUserBadge,
        applyRoleVisibility,
        getRecentUsers,
        API_BASE: '/api',
    };
})();
