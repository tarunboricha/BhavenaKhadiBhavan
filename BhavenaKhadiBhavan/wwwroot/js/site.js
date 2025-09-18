/**
 * Khadi Store - Site JavaScript
 * Common functionality and interactions
 */

(function () {
    'use strict';

    // Global variables
    window.KhadiStore = {
        // Configuration
        config: {
            currency: 'INR',
            locale: 'en-IN',
            dateFormat: 'dd/MM/yyyy',
            autoSaveInterval: 30000, // 30 seconds
            toastDuration: 5000 // 5 seconds
        },

        // Utility functions
        utils: {},

        // Components
        components: {}
    };

    // =========================================
    // Utility Functions
    // =========================================

    /**
     * Format currency amount
     */
    KhadiStore.utils.formatCurrency = function (amount, showSymbol = true) {
        const formatted = parseFloat(amount || 0).toLocaleString('en-IN', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
        return showSymbol ? '₹' + formatted : formatted;
    };

    /**
     * Format date
     */
    KhadiStore.utils.formatDate = function (date, format = 'dd/MM/yyyy') {
        if (!date) return '';
        const d = new Date(date);
        const day = String(d.getDate()).padStart(2, '0');
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const year = d.getFullYear();

        return format.replace('dd', day).replace('MM', month).replace('yyyy', year);
    };

    /**
     * Debounce function
     */
    KhadiStore.utils.debounce = function (func, wait, immediate) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                timeout = null;
                if (!immediate) func.apply(this, args);
            };
            const callNow = immediate && !timeout;
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
            if (callNow) func.apply(this, args);
        };
    };

    /**
     * Show toast notification
     */
    KhadiStore.utils.showToast = function (message, type = 'success', duration = null) {
        // Remove existing toasts of the same type
        $('.toast').remove();

        const iconMap = {
            'success': 'fa-check-circle',
            'error': 'fa-exclamation-circle',
            'warning': 'fa-exclamation-triangle',
            'info': 'fa-info-circle'
        };

        const bgMap = {
            'success': 'bg-success',
            'error': 'bg-danger',
            'warning': 'bg-warning',
            'info': 'bg-info'
        };

        const icon = iconMap[type] || iconMap['info'];
        const bgClass = bgMap[type] || bgMap['info'];

        const toast = $(`
            <div class="toast align-items-center text-white ${bgClass} border-0 position-fixed" 
                 style="top: 20px; right: 20px; z-index: 1055;" role="alert" aria-live="assertive">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="fas ${icon} me-2"></i>${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `);

        $('body').append(toast);
        const bsToast = new bootstrap.Toast(toast[0], {
            delay: duration || KhadiStore.config.toastDuration
        });
        bsToast.show();

        // Auto remove after hide
        toast.on('hidden.bs.toast', function () {
            $(this).remove();
        });
    };

    /**
     * Show loading overlay
     */
    KhadiStore.utils.showLoading = function (message = 'Loading...') {
        if ($('#globalLoading').length > 0) return;

        const loading = $(`
            <div id="globalLoading" class="position-fixed w-100 h-100 d-flex justify-content-center align-items-center" 
                 style="top: 0; left: 0; background: rgba(0,0,0,0.5); z-index: 2000;">
                <div class="bg-white p-4 rounded-3 text-center">
                    <div class="spinner-border text-primary mb-3" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <div class="fw-bold">${message}</div>
                </div>
            </div>
        `);

        $('body').append(loading);
    };

    /**
     * Hide loading overlay
     */
    KhadiStore.utils.hideLoading = function () {
        $('#globalLoading').fadeOut(300, function () {
            $(this).remove();
        });
    };

    /**
     * Confirm dialog with custom styling
     */
    KhadiStore.utils.confirm = function (message, title = 'Confirm', callback = null) {
        if (typeof title === 'function') {
            callback = title;
            title = 'Confirm';
        }

        const result = confirm(message);
        if (callback) {
            callback(result);
        }
        return result;
    };

    /**
     * Validate Indian phone number
     */
    KhadiStore.utils.validatePhone = function (phone) {
        const phoneRegex = /^[6-9]\d{9}$/;
        return phoneRegex.test(phone);
    };

    /**
     * Validate email
     */
    KhadiStore.utils.validateEmail = function (email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    };

    /**
     * Calculate GST
     */
    KhadiStore.utils.calculateGST = function (amount, rate) {
        return (parseFloat(amount) || 0) * (parseFloat(rate) || 0) / 100;
    };

    /**
     * Format phone number for display
     */
    KhadiStore.utils.formatPhone = function (phone) {
        if (!phone || phone.length !== 10) return phone;
        return phone.replace(/(\d{5})(\d{5})/, '$1 $2');
    };

    // =========================================
    // Form Components
    // =========================================

    /**
     * Auto-save form data
     */
    KhadiStore.components.autoSave = function (formSelector, key) {
        const $form = $(formSelector);
        if ($form.length === 0) return;

        // Load saved data
        const saved = localStorage.getItem(`autosave_${key}`);
        if (saved) {
            try {
                const data = JSON.parse(saved);
                Object.keys(data).forEach(name => {
                    const $field = $form.find(`[name="${name}"]`);
                    if ($field.length > 0) {
                        if ($field.is(':checkbox') || $field.is(':radio')) {
                            $field.filter(`[value="${data[name]}"]`).prop('checked', true);
                        } else {
                            $field.val(data[name]);
                        }
                    }
                });
            } catch (e) {
                console.warn('Failed to load autosaved form data:', e);
            }
        }

        // Save data on change
        const saveData = KhadiStore.utils.debounce(function () {
            const formData = {};
            $form.find('input, select, textarea').each(function () {
                const $field = $(this);
                const name = $field.attr('name');
                if (name && !$field.is('[type="password"]')) {
                    if ($field.is(':checkbox')) {
                        formData[name] = $field.is(':checked');
                    } else if ($field.is(':radio')) {
                        if ($field.is(':checked')) {
                            formData[name] = $field.val();
                        }
                    } else {
                        formData[name] = $field.val();
                    }
                }
            });

            localStorage.setItem(`autosave_${key}`, JSON.stringify(formData));
        }, 1000);

        $form.on('change input', saveData);

        // Clear autosave on successful submission
        $form.on('submit', function () {
            localStorage.removeItem(`autosave_${key}`);
        });
    };

    /**
     * Form validation enhancements
     */
    KhadiStore.components.enhanceValidation = function () {
        // Phone number validation
        $('input[type="tel"], input[data-type="phone"]').on('input', function () {
            let value = $(this).val().replace(/\D/g, '');
            if (value.length > 10) {
                value = value.substring(0, 10);
            }
            $(this).val(value);

            if (value.length === 10) {
                $(this).removeClass('is-invalid');
                if (KhadiStore.utils.validatePhone(value)) {
                    $(this).addClass('is-valid');
                } else {
                    $(this).addClass('is-invalid');
                }
            }
        });

        // Email validation
        $('input[type="email"]').on('blur', function () {
            const email = $(this).val();
            if (email) {
                if (KhadiStore.utils.validateEmail(email)) {
                    $(this).removeClass('is-invalid').addClass('is-valid');
                } else {
                    $(this).removeClass('is-valid').addClass('is-invalid');
                }
            }
        });

        // Number formatting
        $('input[data-type="currency"]').on('blur', function () {
            const value = parseFloat($(this).val());
            if (!isNaN(value)) {
                $(this).val(value.toFixed(2));
            }
        });
    };

    // =========================================
    // Search Components
    // =========================================

    /**
     * Enhanced search with debouncing
     */
    KhadiStore.components.enhancedSearch = function (inputSelector, callback, delay = 300) {
        const debouncedSearch = KhadiStore.utils.debounce(callback, delay);

        $(inputSelector).on('input', function () {
            const term = $(this).val().trim();
            if (term.length >= 2 || term.length === 0) {
                debouncedSearch(term);
            }
        });

        // Clear search
        $(inputSelector).after(`
            <button type="button" class="btn btn-outline-secondary btn-clear-search" 
                    style="margin-left: -40px; z-index: 10; position: relative;" 
                    title="Clear search">
                <i class="fas fa-times"></i>
            </button>
        `);

        $('.btn-clear-search').on('click', function () {
            $(inputSelector).val('').trigger('input').focus();
        });
    };

    // =========================================
    // Table Components
    // =========================================

    /**
     * Sortable tables
     */
    KhadiStore.components.sortableTable = function (tableSelector) {
        $(tableSelector + ' th[data-sort]').addClass('sortable-header').css('cursor', 'pointer');

        $(tableSelector).on('click', 'th[data-sort]', function () {
            const $th = $(this);
            const $table = $th.closest('table');
            const column = $th.data('sort');
            const type = $th.data('sort-type') || 'string';
            const currentOrder = $th.data('sort-order') || 'asc';
            const newOrder = currentOrder === 'asc' ? 'desc' : 'asc';

            // Clear other headers
            $table.find('th[data-sort]').removeData('sort-order').find('.sort-icon').remove();

            // Set current header
            $th.data('sort-order', newOrder);
            $th.append(`<i class="fas fa-sort-${newOrder === 'asc' ? 'up' : 'down'} sort-icon ms-1"></i>`);

            // Sort rows
            const $tbody = $table.find('tbody');
            const rows = $tbody.find('tr').toArray();

            rows.sort((a, b) => {
                const aVal = $(a).find('td').eq($th.index()).text().trim();
                const bVal = $(b).find('td').eq($th.index()).text().trim();

                let comparison = 0;
                if (type === 'number' || type === 'currency') {
                    const aNum = parseFloat(aVal.replace(/[₹,]/g, '')) || 0;
                    const bNum = parseFloat(bVal.replace(/[₹,]/g, '')) || 0;
                    comparison = aNum - bNum;
                } else if (type === 'date') {
                    const aDate = new Date(aVal);
                    const bDate = new Date(bVal);
                    comparison = aDate - bDate;
                } else {
                    comparison = aVal.localeCompare(bVal);
                }

                return newOrder === 'asc' ? comparison : -comparison;
            });

            $tbody.empty().append(rows);
        });
    };

    // =========================================
    // Keyboard Shortcuts
    // =========================================

    /**
     * Global keyboard shortcuts
     */
    KhadiStore.components.keyboardShortcuts = function () {
        $(document).on('keydown', function (e) {
            // Don't trigger shortcuts when typing in inputs
            if ($(e.target).is('input, textarea, select')) {
                return;
            }

            switch (true) {
                case e.ctrlKey && e.key === 'n':
                    e.preventDefault();
                    // Navigate to new sale if on sales page
                    if (window.location.pathname.includes('/Sales')) {
                        window.location.href = '/Sales/Create';
                    }
                    break;

                case e.ctrlKey && e.key === 'f':
                    e.preventDefault();
                    $('.search-box, input[type="search"]').first().focus();
                    break;

                case e.key === 'F1':
                    e.preventDefault();
                    if (typeof showHelp === 'function') {
                        showHelp();
                    }
                    break;

                case e.key === 'Escape':
                    // Close modals
                    $('.modal.show').modal('hide');
                    // Clear active elements
                    document.activeElement.blur();
                    break;
            }
        });
    };

    // =========================================
    // Local Storage Management
    // =========================================

    /**
     * Clean old autosave data
     */
    KhadiStore.components.cleanOldData = function () {
        const maxAge = 7 * 24 * 60 * 60 * 1000; // 7 days
        const now = Date.now();

        Object.keys(localStorage).forEach(key => {
            if (key.startsWith('autosave_')) {
                try {
                    const item = localStorage.getItem(key);
                    const data = JSON.parse(item);
                    if (data.timestamp && (now - data.timestamp) > maxAge) {
                        localStorage.removeItem(key);
                    }
                } catch (e) {
                    // If can't parse, assume old format and remove
                    localStorage.removeItem(key);
                }
            }
        });
    };

    // =========================================
    // Initialization
    // =========================================

    $(document).ready(function () {
        // Initialize components
        KhadiStore.components.enhanceValidation();
        KhadiStore.components.keyboardShortcuts();
        KhadiStore.components.cleanOldData();

        // Auto-focus first input
        $('input:visible:first, textarea:visible:first').not('[readonly]').focus();

        // Confirm delete actions
        $(document).on('click', '[data-confirm], .btn-danger, a[href*="/Delete"]', function (e) {
            const message = $(this).data('confirm') || 'Are you sure you want to delete this item?';
            if (!confirm(message)) {
                e.preventDefault();
                return false;
            }
        });

        // Auto-dismiss alerts
        setTimeout(function () {
            $('.alert:not(.alert-permanent)').fadeOut();
        }, 5000);

        // Initialize tooltips
        if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
            const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }

        // Initialize popovers
        if (typeof bootstrap !== 'undefined' && bootstrap.Popover) {
            const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
            popoverTriggerList.map(function (popoverTriggerEl) {
                return new bootstrap.Popover(popoverTriggerEl);
            });
        }

        // Form auto-save for important forms
        if ($('form[data-autosave]').length > 0) {
            $('form[data-autosave]').each(function () {
                const key = $(this).data('autosave') || 'default';
                KhadiStore.components.autoSave(this, key);
            });
        }

        // Enhanced search
        if ($('.enhanced-search').length > 0) {
            $('.enhanced-search').each(function () {
                const callback = window[$(this).data('callback')] || function () { };
                KhadiStore.components.enhancedSearch(this, callback);
            });
        }

        // Sortable tables
        if ($('.sortable-table').length > 0) {
            $('.sortable-table').each(function () {
                KhadiStore.components.sortableTable(this);
            });
        }

        // Add loading states to forms
        $('form').on('submit', function () {
            const $form = $(this);
            const $submitBtn = $form.find('[type="submit"]');

            $submitBtn.prop('disabled', true);
            const originalText = $submitBtn.html();
            $submitBtn.html('<i class="fas fa-spinner fa-spin"></i> Processing...');

            // Re-enable after 10 seconds as fallback
            setTimeout(function () {
                $submitBtn.prop('disabled', false).html(originalText);
            }, 10000);
        });

        // Number input formatting
        $(document).on('input', 'input[type="number"]', function () {
            const value = $(this).val();
            if (value && $(this).data('format') === 'currency') {
                // Format as user types (optional)
            }
        });

        // Add smooth scrolling
        $('a[href^="#"]').on('click', function (e) {
            const target = $(this.getAttribute('href'));
            if (target.length) {
                e.preventDefault();
                $('html, body').animate({
                    scrollTop: target.offset().top - 70
                }, 500);
            }
        });
    });

    // Export to global scope
    window.formatCurrency = KhadiStore.utils.formatCurrency;
    window.showToast = KhadiStore.utils.showToast;
    window.showLoading = KhadiStore.utils.showLoading;
    window.hideLoading = KhadiStore.utils.hideLoading;

})();