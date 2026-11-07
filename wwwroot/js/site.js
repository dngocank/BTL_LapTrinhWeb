// ========================================================
// KITTYPHONE - STOREFRONT & SYSTEM CLIENT-SIDE JAVASCRIPT
// Standard 24hStore.vn Interactive Scripts (No Inline Allowed)
// ========================================================

// 1. Toast Thông Báo Giỏ Hàng & Tương Tác Hệ Thống
function showCartToast(message, isSuccess = true) {
    let container = document.getElementById('kitty-toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'kitty-toast-container';
        container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        container.style.zIndex = '9999';
        document.body.appendChild(container);
    }

    const toastId = 'toast-' + Date.now();
    const borderClass = isSuccess ? 'border-success' : 'border-danger';
    const iconClass = isSuccess ? 'bi-check-circle-fill text-success' : 'bi-exclamation-triangle-fill text-danger';

    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center bg-white border-2 ${borderClass} shadow-lg rounded-3 mb-2" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex align-items-center p-2">
                <div class="p-2">
                    <i class="bi ${iconClass} fs-4"></i>
                </div>
                <div class="toast-body flex-grow-1 py-1 px-2">
                    <div class="fw-bold text-dark" style="font-size: 0.9rem;">${isSuccess ? 'Thông báo KittyPhone' : 'Rất tiếc!'}</div>
                    <div class="text-secondary small">${message}</div>
                    ${isSuccess ? '<div class="mt-1"><a href="/Cart" class="btn btn-sm btn-outline-primary py-0 px-2 fw-semibold" style="font-size: 0.75rem;">Xem giỏ hàng <i class="bi bi-arrow-right"></i></a></div>' : ''}
                </div>
                <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', toastHtml);
    const toastEl = document.getElementById(toastId);
    if (window.bootstrap && bootstrap.Toast) {
        const bsToast = new bootstrap.Toast(toastEl, { delay: 3500 });
        bsToast.show();
        toastEl.addEventListener('hidden.bs.toast', function () {
            toastEl.remove();
        });
    } else {
        setTimeout(function () {
            if (toastEl) toastEl.remove();
        }, 3500);
    }
}

// 2. Nút sao chép văn bản vào bộ nhớ tạm (Clipboard)
$(document).on('click', '.btn-copy-clipboard', function (e) {
    e.preventDefault();
    var txt = $(this).data('copy') || $(this).attr('data-copy');
    if (txt) {
        navigator.clipboard.writeText(txt).then(function() {
            showCartToast('Đã sao chép vào bộ nhớ tạm: ' + txt, true);
        }).catch(function() {
            showCartToast('Vui lòng chọn và sao chép thủ công: ' + txt, false);
        });
    }
});
window.showCartToast = showCartToast;

// 2. Cập Nhật Số Lượng Badge Giỏ Hàng (Header & Mobile Nav)
function updateCartBadges(count) {
    const desktopBadge = document.getElementById('cartBadgeCount');
    const mobileBadge = document.getElementById('mobileCartBadgeCount');

    [desktopBadge, mobileBadge].forEach(function (badge) {
        if (badge) {
            badge.textContent = count;
            badge.style.display = 'inline-flex';
            badge.classList.remove('badge-bump');
            void badge.offsetWidth; // Trigger reflow
            badge.classList.add('badge-bump');
        }
    });
}
window.updateCartBadges = updateCartBadges;

// 3. Hàm Thêm Nhanh Sản Phẩm Vào Giỏ Hàng Bằng AJAX (Global)
function addToCart(productId, quantity = 1, buyNow = false) {
    if (!productId) return;
    const formData = new FormData();
    formData.append('productId', productId);
    formData.append('quantity', quantity);
    formData.append('buyNow', buyNow);

    fetch('/Cart/AddToCart', {
        method: 'POST',
        body: formData,
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        }
    })
    .then(function (response) {
        return response.json();
    })
    .then(function (data) {
        if (data.requireLogin && data.redirectUrl) {
            window.location.href = data.redirectUrl;
            return;
        }
        if (data.success) {
            updateCartBadges(data.count);
            showCartToast(data.message || 'Đã thêm sản phẩm vào giỏ hàng thành công!', true);
        } else {
            showCartToast(data.message || 'Sản phẩm tạm thời hết hàng.', false);
        }
    })
    .catch(function (err) {
        console.warn('Lỗi khi thêm nhanh giỏ hàng qua AJAX, chuyển sang giỏ hàng:', err);
        window.location.href = '/Cart/Checkout?productId=' + productId;
    });
}
window.addToCart = addToCart;

// 4. Mở Modal Thông Tin & Chính Sách
function openPolicyModal(tabKey, evt) {
    if (evt && evt.preventDefault) {
        evt.preventDefault();
    }
    const modalEl = document.getElementById('modalPolicyInfo');
    if (!modalEl) {
        window.location.href = '/Home/Policy?tab=' + encodeURIComponent(tabKey);
        return false;
    }

    const tabBtn = document.getElementById('m-tab-' + tabKey);
    if (tabBtn) {
        if (window.bootstrap && bootstrap.Tab) {
            const bsTab = bootstrap.Tab.getOrCreateInstance(tabBtn);
            bsTab.show();
        } else {
            const allTabs = modalEl.querySelectorAll('#modalPolicyTabs .nav-link');
            const allPanes = modalEl.querySelectorAll('#modalPolicyTabContent .tab-pane');
            allTabs.forEach(function(t) { t.classList.remove('active'); });
            allPanes.forEach(function(p) { p.classList.remove('show', 'active'); });
            tabBtn.classList.add('active');
            const targetPane = document.getElementById('m-pane-' + tabKey);
            if (targetPane) targetPane.classList.add('show', 'active');
        }
    }

    if (window.bootstrap && bootstrap.Modal) {
        const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
        bsModal.show();
    } else if (window.$) {
        $(modalEl).modal('show');
    }
    return false;
}
window.openPolicyModal = openPolicyModal;

// 5. Khởi Tạo & Điều Khiển Carousel Banner Chính (#cpsMainCarousel)
function initMainCarousel() {
    const carouselEl = document.getElementById('cpsMainCarousel');
    if (!carouselEl) return;

    let bsCarousel = null;
    if (window.bootstrap && bootstrap.Carousel) {
        bsCarousel = bootstrap.Carousel.getOrCreateInstance(carouselEl, {
            interval: 4500,
            ride: 'carousel',
            wrap: true,
            touch: true,
            pause: 'hover'
        });
    }

    const prevBtn = carouselEl.querySelector('.carousel-control-prev');
    const nextBtn = carouselEl.querySelector('.carousel-control-next');

    if (prevBtn) {
        prevBtn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (bsCarousel) {
                bsCarousel.prev();
            } else if (window.$) {
                $(carouselEl).carousel('prev');
            }
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (bsCarousel) {
                bsCarousel.next();
            } else if (window.$) {
                $(carouselEl).carousel('next');
            }
        });
    }
}

// 6. Xử Lý Nút "Săn Deal Ngay" Khu Vực Flash Sale
function initSanDealButtons() {
    const dealBtns = document.querySelectorAll('.btn-san-deal');
    dealBtns.forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            // Nếu có cờ data-quick-add="true" thì thêm vào giỏ bằng AJAX, ngược lại chuyển thẳng tới trang chi tiết
            if (btn.getAttribute('data-quick-add') === 'true') {
                e.preventDefault();
                const prodId = btn.getAttribute('data-product-id');
                if (prodId) {
                    addToCart(parseInt(prodId, 10));
                }
            }
        });
    });
}

// 6b. Kích Hoạt Nút "Tư Vấn Miễn Phí" Mở Trợ Lý Ảo KittyBot AI
function initKittyBotTriggers() {
    $(document).on('click', '.btn-trigger-kittybot, .btn-consult-kittybot', function (e) {
        e.preventDefault();
        var prompt = $(this).attr('data-prompt') || $(this).data('prompt') || '';
        if (typeof window.kittyOpenChat === 'function') {
            window.kittyOpenChat(prompt);
        } else {
            var launcher = document.getElementById('kittybot-launcher');
            if (launcher) {
                launcher.click();
            }
        }
    });
}

// 7. Stage Slider Navigation & Tự Động Lướt
let stageAutoScrollTimer = null;
let isStagePaused = false;
const STAGE_AUTO_SCROLL_DELAY = 3000;

function startStageAutoScroll() {
    stopStageAutoScroll();
    stageAutoScrollTimer = setInterval(function () {
        if (isStagePaused) return;

        const track = document.getElementById('stageSliderTrack');
        if (!track) return;

        const visibleItems = track.querySelectorAll('.stage-filter-item:not([style*="display: none"])');
        if (visibleItems.length <= 1) return;

        const step = visibleItems[0].offsetWidth + 12;
        const maxScroll = track.scrollWidth - track.clientWidth;

        if (track.scrollLeft >= maxScroll - 15) {
            track.scrollTo({ left: 0, behavior: 'smooth' });
        } else {
            track.scrollBy({ left: step, behavior: 'smooth' });
        }
    }, STAGE_AUTO_SCROLL_DELAY);
}
window.startStageAutoScroll = startStageAutoScroll;

function stopStageAutoScroll() {
    if (stageAutoScrollTimer) {
        clearInterval(stageAutoScrollTimer);
        stageAutoScrollTimer = null;
    }
}
window.stopStageAutoScroll = stopStageAutoScroll;

function scrollStageSlider(direction) {
    const track = document.getElementById('stageSliderTrack');
    if (track) {
        const visibleItems = track.querySelectorAll('.stage-filter-item:not([style*="display: none"])');
        const step = (visibleItems.length > 0 ? visibleItems[0].offsetWidth + 12 : 240) * direction;
        track.scrollBy({ left: step, behavior: 'smooth' });

        isStagePaused = true;
        setTimeout(function() {
            isStagePaused = false;
        }, 4000);
    }
}
window.scrollStageSlider = scrollStageSlider;

function scrollForYouSlider(direction) {
    const track = document.getElementById('forYouSliderTrack');
    if (track) {
        const scrollAmount = 320 * direction;
        track.scrollBy({ left: scrollAmount, behavior: 'smooth' });
    }
}
window.scrollForYouSlider = scrollForYouSlider;

// 8. Đếm Ngược 4 Chữ Số Realtime (Ngày : Giờ : Phút : Giây)
function initStageCountdown() {
    const targetDate = new Date();
    targetDate.setHours(23, 59, 59, 999);
    targetDate.setDate(targetDate.getDate() + 1);

    function update() {
        const now = new Date().getTime();
        let diff = targetDate.getTime() - now;
        if (diff <= 0) diff = 24 * 3600 * 1000;

        const days = Math.floor(diff / (1000 * 60 * 60 * 24));
        const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
        const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
        const secs = Math.floor((diff % (1000 * 60)) / 1000);

        const elD = document.getElementById('stageCdDay');
        const elH = document.getElementById('stageCdHour');
        const elM = document.getElementById('stageCdMin');
        const elS = document.getElementById('stageCdSec');

        if (elD) elD.textContent = String(days).padStart(2, '0');
        if (elH) elH.textContent = String(hours).padStart(2, '0');
        if (elM) elM.textContent = String(mins).padStart(2, '0');
        if (elS) elS.textContent = String(secs).padStart(2, '0');
    }

    update();
    setInterval(update, 1000);
}

// 9. Đồng Hồ Đếm Ngược Flash Sale Giờ Vàng
function initFlashSaleTimer() {
    const timerEl = document.getElementById('flashSaleTimer');
    if (!timerEl) return;

    const endTime = new Date();
    endTime.setHours(23, 59, 59, 999);

    function tick() {
        const now = new Date().getTime();
        let diff = endTime.getTime() - now;
        if (diff < 0) diff = 0;

        const hours = Math.floor((diff / (1000 * 60 * 60)) % 24);
        const minutes = Math.floor((diff / (1000 * 60)) % 60);
        const seconds = Math.floor((diff / 1000) % 60);

        const hEl = timerEl.querySelector('.timer-hours');
        const mEl = timerEl.querySelector('.timer-minutes');
        const sEl = timerEl.querySelector('.timer-seconds');

        if (hEl) hEl.textContent = String(hours).padStart(2, '0');
        if (mEl) mEl.textContent = String(minutes).padStart(2, '0');
        if (sEl) sEl.textContent = String(seconds).padStart(2, '0');
    }

    tick();
    setInterval(tick, 1000);
}

// 10. Bảng Tính Định Giá Thu Cũ Đổi Mới (Trade-In Calculator)
function calculateTradeIn() {
    const oldVal = parseFloat(document.getElementById('tradeOldDevice')?.value || 21500000);
    const condMult = parseFloat(document.getElementById('tradeCondition')?.value || 1.0);
    const newVal = parseFloat(document.getElementById('tradeNewDevice')?.value || 41990000);

    const actualOldPrice = Math.round(oldVal * condMult);
    const subsidy = 2500000;
    const totalDeduct = actualOldPrice + subsidy;
    const payMore = Math.max(0, newVal - totalDeduct);

    function fmt(n) {
        return n.toLocaleString('vi-VN') + 'đ';
    }

    const elOld = document.getElementById('tradeOldPrice');
    const elDeduct = document.getElementById('tradeTotalDeduct');
    const elPay = document.getElementById('tradePayMore');

    if (elOld) elOld.textContent = fmt(actualOldPrice);
    if (elDeduct) elDeduct.textContent = fmt(totalDeduct);
    if (elPay) elPay.textContent = fmt(payMore);
}
window.calculateTradeIn = calculateTradeIn;

// 11. Góc Bình Chọn Công Nghệ (Tech Poll)
const pollState = {
    votes: [800, 474, 208],
    hasVoted: false
};

function initPollState() {
    const saved = localStorage.getItem('kitty_tech_poll');
    if (saved) {
        pollState.hasVoted = true;
        const msg = document.getElementById('pollAlertMessage');
        if (msg) msg.style.display = 'block';
        highlightVotedOption(saved);
    }
}

function castPollVote(optId) {
    if (pollState.hasVoted) {
        alert('Bạn đã tham gia bình chọn rồi! Mã voucher của bạn là: FLAGSHIP500');
        return;
    }

    pollState.votes[optId - 1]++;
    pollState.hasVoted = true;
    localStorage.setItem('kitty_tech_poll', optId);

    const total = pollState.votes[0] + pollState.votes[1] + pollState.votes[2];
    const p1 = Math.round((pollState.votes[0] / total) * 100);
    const p2 = Math.round((pollState.votes[1] / total) * 100);
    const p3 = 100 - p1 - p2;

    const b1 = document.getElementById('pollBar1');
    const pc1 = document.getElementById('pollPercent1');
    if (b1) b1.style.width = p1 + '%';
    if (pc1) pc1.textContent = p1 + '%';

    const b2 = document.getElementById('pollBar2');
    const pc2 = document.getElementById('pollPercent2');
    if (b2) b2.style.width = p2 + '%';
    if (pc2) pc2.textContent = p2 + '%';

    const b3 = document.getElementById('pollBar3');
    const pc3 = document.getElementById('pollPercent3');
    if (b3) b3.style.width = p3 + '%';
    if (pc3) pc3.textContent = p3 + '%';

    const tv = document.getElementById('pollTotalVotes');
    if (tv) tv.textContent = total.toLocaleString('vi-VN');

    const msg = document.getElementById('pollAlertMessage');
    if (msg) msg.style.display = 'block';

    highlightVotedOption(optId);

    if (window.showCartToast) {
        window.showCartToast('Đã ghi nhận bình chọn thành công! Bạn nhận được Voucher 500k', true);
    }
}
window.castPollVote = castPollVote;

function highlightVotedOption(optId) {
    const cards = document.querySelectorAll('.poll-card');
    cards.forEach(function (c, idx) {
        if (idx === (optId - 1)) {
            c.style.borderColor = '#0284c7';
            c.style.boxShadow = '0 4px 12px rgba(2, 132, 199, 0.15)';
        } else {
            c.style.borderColor = '#e2e8f0';
            c.style.boxShadow = 'none';
        }
    });
}

// 12. DOMContentLoaded Initialization Orchestrator
document.addEventListener("DOMContentLoaded", function () {
    // 1. Khởi tạo Carousel Banner
    initMainCarousel();

    // 2. Khởi tạo nút Săn Deal
    initSanDealButtons();

    // 2b. Khởi tạo nút Tư Vấn KittyBot AI trên Banner
    initKittyBotTriggers();

    // 3. Khởi tạo Countdown Timers
    initStageCountdown();
    initFlashSaleTimer();

    // 4. Khởi tạo Tech Poll
    initPollState();

    // 5. Cấu hình Stage Slider Tabs & Tự Động Lướt
    const stageSection = document.getElementById('dealChongDeal');
    if (stageSection) {
        stageSection.addEventListener('mouseenter', function () {
            isStagePaused = true;
        });
        stageSection.addEventListener('mouseleave', function () {
            isStagePaused = false;
        });
        stageSection.addEventListener('touchstart', function () {
            isStagePaused = true;
        }, { passive: true });
        stageSection.addEventListener('touchend', function () {
            setTimeout(function () { isStagePaused = false; }, 3000);
        }, { passive: true });

        startStageAutoScroll();

        const pills = document.querySelectorAll('#stageTabsList .cps-stage-pill');
        const track = document.getElementById('stageSliderTrack');

        pills.forEach(function (pill) {
            pill.addEventListener('click', function () {
                pills.forEach(function (p) { p.classList.remove('active'); });
                this.classList.add('active');
                const filter = this.getAttribute('data-stage-filter');
                const items = document.querySelectorAll('#stageSliderTrack .stage-filter-item');

                items.forEach(function (item) {
                    const tags = (item.getAttribute('data-tags') || '').split(' ');
                    if (filter === 'all' || tags.indexOf(filter) !== -1) {
                        item.style.display = '';
                    } else {
                        item.style.display = 'none';
                    }
                });

                if (track) {
                    track.scrollTo({ left: 0, behavior: 'smooth' });
                }

                isStagePaused = false;
                startStageAutoScroll();
            });
        });
    }

    // 6. Bắt Sự Kiện Submit Form Giỏ Hàng Qua AJAX
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (!form || !form.matches) return;

        const action = form.getAttribute('action') || '';
        const isCartForm = action.includes('/Cart/AddToCart') || 
                           action.includes('/Cart/Add') ||
                           form.matches('form[action*="/Cart/AddToCart"]') || 
                           form.matches('form[action*="/Cart/Add"]');

        if (!isCartForm) return;

        const buyNowInput = form.querySelector('input[name="buyNow"]');
        if (buyNowInput && (buyNowInput.value === 'true' || buyNowInput.value === 'True')) {
            return; // Cho phép submit bình thường để tới Checkout
        }

        e.preventDefault();

        const submitBtn = form.querySelector('button[type="submit"]');
        const originalContent = submitBtn ? submitBtn.innerHTML : null;

        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm text-primary" role="status"></span>';
        }

        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
        .then(function (response) {
            return response.json();
        })
        .then(function (data) {
            if (data.requireLogin && data.redirectUrl) {
                window.location.href = data.redirectUrl;
                return;
            }
            if (data.success) {
                updateCartBadges(data.count);
                showCartToast(data.message || 'Đã thêm sản phẩm vào giỏ hàng!', true);
                if (submitBtn) {
                    submitBtn.innerHTML = '<i class="bi bi-check-lg text-success"></i>';
                }
            } else {
                showCartToast(data.message || 'Không thể thêm vào giỏ hàng', false);
                if (submitBtn && originalContent) {
                    submitBtn.innerHTML = originalContent;
                }
            }
        })
        .catch(function (err) {
            console.error('Lỗi khi thêm vào giỏ hàng:', err);
            form.submit();
        })
        .finally(function () {
            if (submitBtn) {
                setTimeout(function () {
                    submitBtn.disabled = false;
                    if (originalContent) {
                        submitBtn.innerHTML = originalContent;
                    }
                }, 1000);
            }
        });
    });
});

/* ========================================================
   WARRANTY LOOKUP QR MODAL (Global Handler)
   ======================================================== */
window.showLookupQrModal = function (ticketCode, amount) {
    var syntax = 'BH ' + ticketCode;
    var qrUrl = 'https://img.vietqr.io/image/970454-0396166509-compact2.png?amount=' + amount + '&addInfo=' + encodeURIComponent(syntax) + '&accountName=DUONG%20NGOC%20ANH';
    var imgEl = document.getElementById('imgModalLookupQr');
    var amountEl = document.getElementById('txtModalLookupAmount');
    var syntaxEl = document.getElementById('badgeModalLookupSyntax');
    var modalEl = document.getElementById('modalLookupQr');

    if (imgEl) imgEl.src = qrUrl;
    if (amountEl) amountEl.textContent = Math.round(amount).toLocaleString('vi-VN') + 'đ';
    if (syntaxEl) syntaxEl.textContent = syntax;
    if (modalEl && typeof bootstrap !== 'undefined') {
        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
    }
};

/* ========================================================
   ADMIN LOGIN & 2FA HELPERS
   ======================================================== */
window.fillCreds = function (u, p) {
    var uInput = document.getElementById('inputUsername');
    var pInput = document.getElementById('inputPassword');
    if (uInput) uInput.value = u;
    if (pInput) pInput.value = p;
};

document.addEventListener('DOMContentLoaded', function () {
    var timerDisplay = document.getElementById('otpTimer');
    if (timerDisplay) {
        var duration = 5 * 60;
        var timer = setInterval(function () {
            var minutes = parseInt(duration / 60, 10);
            var seconds = parseInt(duration % 60, 10);
            minutes = minutes < 10 ? "0" + minutes : minutes;
            seconds = seconds < 10 ? "0" + seconds : seconds;
            timerDisplay.textContent = minutes + ":" + seconds;
            if (--duration < 0) {
                clearInterval(timer);
                timerDisplay.textContent = "Hết hạn";
            }
        }, 1000);
    }

    var btnAutoFill = document.getElementById('btnAutoFillOtp');
    if (btnAutoFill) {
        btnAutoFill.addEventListener('click', function () {
            var otp = this.getAttribute('data-otp');
            var otpInput = document.getElementById('otpInput');
            if (otp && otpInput) {
                otpInput.value = otp;
                otpInput.focus();
            }
        });
    }

    // Khởi tạo tương tác trang Thanh toán (Checkout) nếu đang ở trang /Cart/Checkout
    initCheckoutPage();

    // Khởi tạo hệ thống cập nhật Badge thông báo đỏ (Zalo / Messenger Style)
    initRealtimeBadgeNotifications();
});

/* ========================================================
   CHECKOUT SHIPPING & PAYMENT LOGIC (ONE-PAGE CHECKOUT)
   ======================================================== */
function initCheckoutPage() {
    var checkoutForm = document.getElementById('checkoutForm');
    if (!checkoutForm) return;

    var $ = window.jQuery;
    if (!$) return;

    function formatVND(num) {
        return Math.round(num).toLocaleString('vi-VN') + 'đ';
    }

    var subTotalAmount = parseFloat($('#hdnSubTotal').val()) || 0;
    var discountAmount = parseFloat($('#hdnDiscountAmount').val()) || 0;
    var shippingFeeAmount = parseFloat($('#hdnShippingFee').val()) || 0;
    var currentTotal = subTotalAmount;

    // 1. Chuyển đổi phương thức nhận hàng (Giao hàng tận nhà vs Lấy tại cửa hàng)
    function selectDeliveryMethod(method) {
        $('#hdnDeliveryMethod').val(method);
        $('#hdnShippingMethod').val(method);

        if (method === 'StorePickup') {
            $('#btnMethodStore').addClass('active');
            $('#btnMethodHome').removeClass('active');
            $('#boxStorePickupDetails').slideDown();
            $('#boxHomeDeliverySection').slideUp();

            // Cập nhật thông tin mô tả thanh toán 100% phù hợp nhận máy tại showroom
            $('#txtBankTransferDesc').text('Thanh toán toàn bộ 100% qua mã QR ngân hàng hoặc ZaloPay. Đơn hàng sẽ được chuẩn bị sẵn sàng tại showroom để quý khách ghé nhận máy ngay sau khi nhận tiền.');

            // Lấy tại cửa hàng: phí ship = 0đ, ẩn/bỏ bắt buộc nhập địa chỉ nhà riêng
            shippingFeeAmount = 0;
            $('#hdnShippingFee').val(0);
            $('#hdnShippingCarrier').val('store_pickup');
            $('#hdnShippingCarrierName').val('Nhận tại Showroom KittyPhone');
            $('#hdnShippingServiceType').val('Nhận tại Showroom 123 Thái Hà');
            $('#txtAddressInput').removeAttr('required');

            $('#txtShippingFeeDisplay').html('<span class="text-success fw-bold">0đ (Tại cửa hàng)</span>');
            recalculateGrandTotal();
        } else {
            $('#btnMethodHome').addClass('active');
            $('#btnMethodStore').removeClass('active');
            $('#boxStorePickupDetails').slideUp();
            $('#boxHomeDeliverySection').slideDown();

            // Cập nhật thông tin mô tả thanh toán 100% cho giao hàng tận nhà
            $('#txtBankTransferDesc').text('Thanh toán toàn bộ 100% qua mã QR ngân hàng hoặc ZaloPay. Đơn hàng sẽ được duyệt và xuất kho giao siêu tốc ngay sau khi nhận tiền.');

            // Giao hàng tận nhà: khôi phục bắt buộc địa chỉ nhận hàng
            $('#txtAddressInput').attr('required', 'required');

            // Tính cước theo khoảng cách qua AJAX
            var activeCard = $('.carrier-option-card.selected');
            if (activeCard.length > 0) {
                selectCarrier(activeCard[0]);
            } else {
                recalculateShipping();
            }
        }
    }

    // Gắn sự kiện click chọn phương thức nhận hàng
    $(document).on('click', '.delivery-method-card', function () {
        var method = $(this).data('delivery-method') || ($(this).attr('id') === 'btnMethodStore' ? 'StorePickup' : 'HomeDelivery');
        selectDeliveryMethod(method);
    });

    // 2. Chọn nhà vận chuyển (Carrier Option Card)
    function selectCarrier(element) {
        $('.carrier-option-card').removeClass('selected');
        $(element).addClass('selected');

        var carrier = $(element).data('carrier');
        var name = $(element).data('name');
        var fee = parseFloat($(element).data('fee')) || 0;
        var service = $(element).data('service');

        $('#hdnShippingCarrier').val(carrier);
        $('#hdnShippingCarrierName').val(name);
        $('#hdnShippingFee').val(fee);
        $('#hdnShippingServiceType').val(service);

        shippingFeeAmount = fee;
        if (fee === 0) {
            $('#txtShippingFeeDisplay').html('<span class="text-success fw-bold">0đ (Freeship)</span>');
        } else {
            $('#txtShippingFeeDisplay').html('<span class="text-dark fw-bold">' + formatVND(fee) + '</span>');
        }

        recalculateGrandTotal();
    }

    $(document).on('click', '.carrier-option-card', function () {
        selectCarrier(this);
    });

    // 3. Xử lý dropdown chọn nhanh Quận/Huyện Hà Nội
    $('#cboDistrictQuick').on('change', function () {
        var sel = $(this).find('option:selected');
        var km = parseFloat(sel.data('km')) || 5.0;
        var districtName = sel.val();

        $('#txtDistanceKmInput').val(km);
        $('#hdnShippingDistanceKm').val(km);

        var currentAddr = $('#txtAddressInput').val();
        if (!currentAddr || currentAddr.indexOf('Hà Nội') < 0) {
            if (districtName === 'Tỉnh khác') {
                $('#txtAddressInput').val('TP. Hồ Chí Minh / Tỉnh thành');
            } else {
                $('#txtAddressInput').val('Quận ' + districtName + ', Hà Nội');
            }
        }

        recalculateShipping();
    });

    // 4. Xử lý thay đổi khoảng cách km thủ công
    var distanceTimer = null;
    $('#txtDistanceKmInput').on('input change', function () {
        var km = parseFloat($(this).val()) || 5.0;
        $('#hdnShippingDistanceKm').val(km);

        clearTimeout(distanceTimer);
        distanceTimer = setTimeout(function () {
            recalculateShipping();
        }, 350);
    });

    $('#btnRecalculateShipping').on('click', function (e) {
        e.preventDefault();
        recalculateShipping();
    });

    // 5. AJAX Tính cước vận chuyển tự động
    function recalculateShipping() {
        if ($('#hdnDeliveryMethod').val() === 'StorePickup') return;

        var distance = parseFloat($('#txtDistanceKmInput').val()) || 5.0;
        var district = $('#cboDistrictQuick').val() || 'Cầu Giấy';
        var address = $('#txtAddressInput').val() || 'Hà Nội';
        var currentCarrier = $('#hdnShippingCarrier').val();

        $.ajax({
            url: '/Cart/CalculateShipping',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                distanceKm: distance,
                district: district,
                address: address,
                subTotal: subTotalAmount,
                discountAmount: discountAmount,
                carrierId: currentCarrier,
                deliveryMethod: $('#hdnDeliveryMethod').val() || 'HomeDelivery',
                shippingMethod: $('#hdnShippingMethod').val() || 'HomeDelivery'
            }),
            success: function (res) {
                if (res && res.success) {
                    if (res.deliveryMethod === 'StorePickup') {
                        shippingFeeAmount = 0;
                        $('#hdnShippingFee').val(0);
                        $('#txtShippingFeeDisplay').html('<span class="text-success fw-bold">0đ (Tại cửa hàng)</span>');
                        recalculateGrandTotal();
                        return;
                    }

                    if (res.options && res.options.length > 0) {
                        renderCarrierOptions(res.options);
                    } else {
                        shippingFeeAmount = res.shippingFee || 0;
                        $('#hdnShippingFee').val(shippingFeeAmount);
                        if (shippingFeeAmount === 0) {
                            $('#txtShippingFeeDisplay').html('<span class="text-success fw-bold">0đ (Freeship)</span>');
                        } else {
                            $('#txtShippingFeeDisplay').html('<span class="text-dark fw-bold">' + formatVND(shippingFeeAmount) + '</span>');
                        }
                        recalculateGrandTotal();
                    }
                }
            },
            error: function (xhr, status, err) {
                console.warn('Không thể tải cước shipping động từ CartController, giữ phí hiện tại:', err);
            }
        });
    }

    // 6. Hiển thị danh sách hãng vận chuyển khả dụng từ response
    function renderCarrierOptions(options) {
        var container = $('#carrierCardsContainer');
        var currentCarrier = $('#hdnShippingCarrier').val();
        container.empty();

        var foundSelected = false;

        options.forEach(function (opt) {
            var isSelected = (!foundSelected && (opt.carrierCode === currentCarrier || !currentCarrier));
            if (isSelected) foundSelected = true;

            var feeBadge = opt.finalFee === 0
                ? '<span class="badge bg-success-subtle text-success fee-badge">0đ (Freeship)</span>'
                : '<span class="badge bg-light text-dark border fee-badge">' + formatVND(opt.finalFee) + '</span>';

            var subsidyNote = opt.storeSubsidy > 0
                ? '<span class="text-success ms-1"><i class="bi bi-tag-fill"></i> Trợ giá ' + formatVND(opt.storeSubsidy) + '</span>'
                : '';

            var col = $(
                '<div class="col-md-6">' +
                    '<div class="carrier-option-card ' + (isSelected ? 'selected' : '') + ' p-2 border rounded-3 cursor-pointer"' +
                         ' data-carrier="' + opt.carrierCode + '"' +
                         ' data-name="' + opt.carrierName + '"' +
                         ' data-fee="' + opt.finalFee + '"' +
                         ' data-service="' + opt.serviceType + '">' +
                        '<div class="d-flex justify-content-between align-items-start mb-1">' +
                            '<span class="fw-bold small text-dark">' + opt.carrierName + '</span>' +
                            feeBadge +
                        '</div>' +
                        '<div class="text-muted" style="font-size: 11px;">' +
                            opt.estimatedTime + ' • ' + opt.serviceType + subsidyNote +
                        '</div>' +
                    '</div>' +
                '</div>'
            );
            container.append(col);

            if (isSelected) {
                selectCarrier(col.find('.carrier-option-card')[0]);
            }
        });
    }

    // 7. Cập nhật tổng thanh toán và tóm tắt phương thức chi trả
    function recalculateGrandTotal() {
        currentTotal = Math.max(0, subTotalAmount - discountAmount) + shippingFeeAmount;
        $('#txtFinalTotal').text(formatVND(currentTotal));
        updatePaymentSummary();
    }

    function updatePaymentSummary() {
        var val = $('input[name="PaymentMethod"]:checked').val();
        var isStorePickup = ($('#hdnShippingMethod').val() === 'StorePickup' || $('#hdnDeliveryMethod').val() === 'StorePickup');

        if (val === 'COD') {
            $('#boxBankQrPreview').slideUp();
            $('#summaryDepositLabel').text('Mức cọc cần thanh toán (50%):');
            var dep = Math.round(currentTotal * 0.5 / 10000) * 10000;
            $('#summaryDepositValue').text(formatVND(dep));
            $('#summaryRemainingLabel').text(isStorePickup ? 'Còn lại khi nhận máy tại showroom:' : 'Còn lại khi nhận hàng:');
            $('#summaryRemainingValue').text(formatVND(currentTotal - dep));
            $('#txtDeposit50').text(formatVND(dep).replace('đ', ''));
            $('#txtRemaining50').text(formatVND(currentTotal - dep).replace('đ', ''));
        } else if (val === 'BankTransfer') {
            $('#boxBankQrPreview').slideDown();
            var qrUrl = 'https://img.vietqr.io/image/970454-0396166509-compact2.png?amount=' + Math.round(currentTotal) + '&addInfo=' + encodeURIComponent('KITTYPHONE DAT HANG') + '&accountName=DUONG%20NGOC%20ANH';
            $('#imgCheckoutBankQr').attr('src', qrUrl);
            $('#txtCheckoutBankQrAmount').text(formatVND(currentTotal));

            $('#summaryDepositLabel').text('Thanh toán toàn bộ (100%):');
            $('#summaryDepositValue').text(formatVND(currentTotal));
            $('#summaryRemainingLabel').text(isStorePickup ? 'Còn lại khi nhận máy tại showroom:' : 'Còn lại khi nhận hàng:');
            $('#summaryRemainingValue').text('0đ (Đã thanh toán đủ)');
        } else if (val === 'Installment') {
            $('#boxBankQrPreview').slideUp();
            calculateInstallment();
        }
    }

    function calculateInstallment() {
        var prepayPercent = parseInt($('#inputPrepayPercent').val(), 10) || 30;
        var months = parseInt($('#inputInstallmentMonths').val(), 10) || 6;

        var prepayAmount = Math.round(currentTotal * (prepayPercent / 100) / 10000) * 10000;
        var loanAmount = Math.max(0, currentTotal - prepayAmount);
        var monthlyAmount = months > 0 ? Math.round(loanAmount / months / 1000) * 1000 : 0;

        $('#txtPrepayAmount').text(formatVND(prepayAmount));
        $('#txtLoanAmount').text(formatVND(loanAmount));
        $('#txtMonthlyAmount').text(formatVND(monthlyAmount) + ' / tháng');

        if ($('input[name="PaymentMethod"]:checked').val() === 'Installment') {
            $('#summaryDepositLabel').text('Tiền trả trước (' + prepayPercent + '%):');
            $('#summaryDepositValue').text(formatVND(prepayAmount));
            $('#summaryRemainingLabel').text('Góp mỗi tháng (' + months + ' tháng):');
            $('#summaryRemainingValue').text(formatVND(monthlyAmount) + ' / tháng');
        }
    }

    // 8. Khuyến mãi & Voucher
    $('#cboPromotion').on('change', function () {
        var promoId = $(this).val();
        $('#promoErrorBox').hide();

        if (!promoId) {
            discountAmount = 0;
            $('#hdnDiscountAmount').val(0);
            $('#hdnDiscountPercent').val(0);
            $('#rowDiscount').hide();
            $('#promoDescriptionBox').slideUp();
            recalculateGrandTotal();
            recalculateShipping();
            return;
        }

        $.get('/Cart/CalculateDiscount', { promotionId: promoId, subTotal: subTotalAmount }, function (res) {
            if (res.success) {
                discountAmount = res.discountAmount;
                $('#hdnDiscountAmount').val(res.discountAmount);
                $('#hdnDiscountPercent').val(res.discountPercent);
                $('#txtDiscountPercent').text(res.discountPercent);
                $('#txtDiscountAmount').text('-' + formatVND(res.discountAmount));
                $('#rowDiscount').show();

                var desc = $('#cboPromotion option:selected').data('desc');
                if (desc) {
                    $('#promoDescriptionText').text(desc);
                    $('#promoDescriptionBox').slideDown();
                } else {
                    $('#promoDescriptionBox').slideUp();
                }
            } else {
                $('#promoErrorText').text(res.message);
                $('#promoErrorBox').slideDown();
                $('#promoDescriptionBox').slideUp();

                discountAmount = 0;
                $('#hdnDiscountAmount').val(0);
                $('#hdnDiscountPercent').val(0);
                $('#rowDiscount').hide();
            }
            recalculateGrandTotal();
            recalculateShipping();
        }).fail(function () {
            $('#promoErrorText').text('Lỗi kiểm tra mã khuyến mãi. Vui lòng thử lại.');
            $('#promoErrorBox').slideDown();
        });
    });

    // 9. Lắng nghe chọn phương thức thanh toán
    $('input[name="PaymentMethod"]').on('change', function () {
        var val = $(this).val();
        $('.payment-option-card').removeClass('selected');
        if (val === 'COD') {
            $('#cardCod').addClass('selected');
            $('#boxInstallmentDetails').slideUp();
        } else if (val === 'BankTransfer') {
            $('#cardBank').addClass('selected');
            $('#boxInstallmentDetails').slideUp();
        } else if (val === 'Installment') {
            $('#cardInstallment').addClass('selected');
            $('#boxInstallmentDetails').slideDown();
        }
        updatePaymentSummary();
    });

    $('.btn-prepay').on('click', function () {
        $('.btn-prepay').removeClass('active');
        $(this).addClass('active');
        $('#inputPrepayPercent').val($(this).data('percent'));
        calculateInstallment();
    });

    $('.btn-term').on('click', function () {
        $('.btn-term').removeClass('active btn-primary').addClass('btn-outline-secondary');
        $(this).removeClass('btn-outline-secondary').addClass('active btn-primary');
        $('#inputInstallmentMonths').val($(this).data('months'));
        calculateInstallment();
    });

    // 10. Kích hoạt tính cước ban đầu
    recalculateShipping();
    updatePaymentSummary();
}

/* ========================================================
   REALTIME BADGE NOTIFICATIONS (ZALO / MESSENGER STYLE)
   ======================================================== */
function initRealtimeBadgeNotifications() {
    function updateBadge(selector, count, showPulse) {
        var el = $(selector);
        if (el.length === 0) return;
        if (count > 0) {
            el.text(count > 99 ? '99+' : count).show();
            if (showPulse) {
                el.addClass('badge-pulse');
            } else {
                el.removeClass('badge-pulse');
            }
        } else {
            el.hide().removeClass('badge-pulse');
        }
    }

    function fetchBadgeCounts() {
        $.ajax({
            url: '/api/notifications/badge-counts',
            type: 'GET',
            dataType: 'json',
            success: function (res) {
                if (res && res.success && res.data) {
                    var d = res.data;

                    // 1. Quản lý nội bộ (Admin / Sales / Warehouse / Tech)
                    var ordersNeedingAction = (d.ordersNeedingAction !== undefined) ? d.ordersNeedingAction : (d.pendingOrders + d.cancelRequestedOrders);
                    updateBadge('#badgeSidebarOrders', ordersNeedingAction, ordersNeedingAction > 0);
                    updateBadge('#badgeSidebarReviews', d.unrepliedReviews, false);
                    updateBadge('#badgeSidebarLowStock', d.lowStockCount, false);
                    updateBadge('#badgeSidebarWorkbench', d.activeWarrantyTickets, false);
                    updateBadge('#badgeSidebarTickets', d.activeWarrantyTickets, false);
                    updateBadge('#badgeTopbarAlerts', d.totalAdminAlerts, d.totalAdminAlerts > 0);

                    // 2. Khách hàng cá nhân (Customer)
                    var orderTitle = 'Bạn có ' + (d.customerActiveOrders || 0) + ' đơn hàng đang chờ duyệt/xử lý';
                    $('#badgeCustomerOrders').attr('title', orderTitle);
                    updateBadge('#badgeCustomerOrders', d.customerActiveOrders, d.customerActiveOrders > 0);

                    var warrantyCount = (d.customerActiveWarranty && d.customerActiveWarranty > 0) 
                        ? d.customerActiveWarranty 
                        : (d.customerWarrantedDevices || 0);
                    var warrantyTitle = (d.customerActiveWarranty && d.customerActiveWarranty > 0)
                        ? ('Bạn có ' + d.customerActiveWarranty + ' phiếu sửa chữa đang tiếp nhận/xử lý')
                        : ('Bạn có ' + (d.customerWarrantedDevices || 0) + ' thiết bị đang còn hạn bảo hành chính hãng');
                    $('#badgeCustomerWarranty').attr('title', warrantyTitle);
                    updateBadge('#badgeCustomerWarranty', warrantyCount, d.customerActiveWarranty > 0);

                    if (d.customerCartCount !== undefined) {
                        updateBadge('#cartBadgeCount', d.customerCartCount, false);
                    }
                }
            },
            error: function () {
                // Giữ trạng thái hiện tại khi mất kết nối tạm thời
            }
        });
    }

    // Polling định kỳ mỗi 25 giây để đồng bộ số lượng thông báo
    setInterval(fetchBadgeCounts, 25000);
}



