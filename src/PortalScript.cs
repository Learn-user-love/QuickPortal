// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System.Text;
using System.Web.Script.Serialization;

namespace QuickPortal
{
    public static class PortalScript
    {
        public static string Build(AppConfig config, bool autoSubmit)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string username = serializer.Serialize(config.Username ?? "");
            string password = serializer.Serialize(config.GetPassword());
            string autoFill = config.AutoFill ? "true" : "false";
            string doSubmit = autoSubmit ? "true" : "false";

            StringBuilder sb = new StringBuilder();
            sb.Append(@"(function () {
  if (window.__quickPortalState && window.__quickPortalState.finished) return;
  var cfg = {
    username: ");
            sb.Append(username);
            sb.Append(@",
    password: ");
            sb.Append(password);
            sb.Append(@",
    autoFill: ");
            sb.Append(autoFill);
            sb.Append(@",
    autoSubmit: ");
            sb.Append(doSubmit);
            sb.Append(@"
  };
  var state = window.__quickPortalState = { started: Date.now(), finished: false, submitted: false, retries: 0 };
  function send(eventName, message) {
    try {
      if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage({ source: 'QuickPortal', event: eventName, message: message || '' });
      }
    } catch (e) { }
  }
  function isVisible(el) {
    try {
      if (!el) return false;
      var style = window.getComputedStyle(el);
      if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
      var rect = el.getBoundingClientRect();
      return rect.width > 1 && rect.height > 1;
    } catch (e) { return false; }
  }
  function isEditable(el) {
    if (!el || el.disabled || el.readOnly) return false;
    var type = (el.getAttribute('type') || 'text').toLowerCase();
    return type !== 'hidden' && type !== 'submit' && type !== 'button' && type !== 'checkbox' && type !== 'radio' && type !== 'file';
  }
  function attrText(el) {
    return ((el.getAttribute('name') || '') + ' ' + (el.getAttribute('id') || '') + ' ' +
      (el.getAttribute('placeholder') || '') + ' ' + (el.getAttribute('aria-label') || '') + ' ' +
      (el.getAttribute('autocomplete') || '')).toLowerCase();
  }
  function setValue(el, value) {
    try {
      var proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
      var setter = Object.getOwnPropertyDescriptor(proto, 'value');
      if (setter && setter.set) setter.set.call(el, value); else el.value = value;
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
      el.dispatchEvent(new Event('blur', { bubbles: true }));
    } catch (e) {
      try { el.value = value; } catch (e2) { }
    }
  }
  function scoreUserInput(el, pwd) {
    var score = 0;
    var t = attrText(el);
    if (/user|account|login|name|学号|账号|帐号|用户名|用户|工号|手机|电话|邮箱/.test(t)) score += 60;
    if (/search|searchbox|captcha|verify|code|验证码|搜索|查询/.test(t)) score -= 80;
    var type = (el.getAttribute('type') || 'text').toLowerCase();
    if (type === 'email' || type === 'tel') score += 15;
    if (pwd && pwd.form && el.form === pwd.form) score += 35;
    if (pwd && el.compareDocumentPosition(pwd) & Node.DOCUMENT_POSITION_FOLLOWING) score += 10;
    if (el.value && el.value.length > 0) score += 3;
    return score;
  }
  function findFields() {
    var inputs = Array.prototype.slice.call(document.querySelectorAll('input, textarea'));
    var visible = inputs.filter(function (el) { return isVisible(el) && isEditable(el); });
    var pwd = null;
    for (var i = 0; i < visible.length; i++) {
      if ((visible[i].getAttribute('type') || '').toLowerCase() === 'password') { pwd = visible[i]; break; }
    }
    var user = null;
    var best = -1;
    for (var j = 0; j < visible.length; j++) {
      var el = visible[j];
      var type = (el.getAttribute('type') || 'text').toLowerCase();
      if (type === 'password' || type === 'checkbox' || type === 'radio' || type === 'submit' || type === 'button') continue;
      var s = scoreUserInput(el, pwd);
      if (s > best) { best = s; user = el; }
    }
    return { user: best >= 25 ? user : null, pwd: pwd, count: visible.length };
  }
  function hasCaptcha() {
    var inputs = Array.prototype.slice.call(document.querySelectorAll('input'));
    for (var i = 0; i < inputs.length; i++) {
      if (!isVisible(inputs[i])) continue;
      var t = attrText(inputs[i]);
      if (/captcha|verifycode|checkcode|验证码|校验码|短信码|动态码/.test(t)) return true;
    }
    var imgs = Array.prototype.slice.call(document.querySelectorAll('img'));
    for (var j = 0; j < imgs.length; j++) {
      if (!isVisible(imgs[j])) continue;
      var src = (imgs[j].getAttribute('src') || '').toLowerCase();
      if (src.indexOf('captcha') >= 0 || src.indexOf('verify') >= 0 || src.indexOf('code') >= 0) return true;
    }
    return false;
  }
  function pageLooksOnline() {
    var body = document.body;
    if (!body) return '';
    var text = (body.innerText || '').replace(/\s+/g, ' ');
    if (text.length > 30000) text = text.substring(0, 30000);
    var pwdVisible = false;
    var pwds = document.querySelectorAll('input[type=password]');
    for (var p = 0; p < pwds.length; p++) {
      if (isVisible(pwds[p])) { pwdVisible = true; break; }
    }
    if (!pwdVisible && /(认证成功|登录成功|已成功连接|接入成功|successfully connected|logged in successfully)/i.test(text)) {
      return 'success-text';
    }
    if (pwdVisible) return '';
    var nodes = Array.prototype.slice.call(document.querySelectorAll('button, a, [role=button]'));
    for (var i = 0; i < nodes.length; i++) {
      if (!isVisible(nodes[i])) continue;
      var t = (nodes[i].innerText || nodes[i].value || '').replace(/\s+/g, ' ').trim();
      if (/^(注销|退出登录|退出|断开连接|断开|logout|sign out)$/i.test(t)) return 'logout:' + t;
    }
    if (text.length < 6000 && /(已认证|已连接|当前在线|在线状态|已登录)/.test(text)) return 'status-text';
    return '';
  }
  function scoreButton(el, pwd) {
    if (!isVisible(el)) return -1;
    if (el.disabled || el.getAttribute('aria-disabled') === 'true') return -1;
    var text = ((el.innerText || el.value || el.getAttribute('aria-label') || el.getAttribute('title') || '') + '').replace(/\s+/g, ' ').trim().toLowerCase();
    if (!text || text.length > 40) return -1;
    if (/注销|退出|断开|取消|忘记|注册|下载|帮助|客服|切换|二维码|扫码|其他方式/.test(text)) return -1;
    var score = 0;
    if (text === '登录' || text === '立即登录' || text === 'login' || text === 'sign in') score = 100;
    else if (/立即登录|登录上网|登录|认证|上线/.test(text)) score = 80;
    else if (/^login|sign in|log in|connect|认证/.test(text)) score = 70;
    else if (/连接|确定|提交|submit|connect/.test(text)) score = 45;
    if (!score) return -1;
    var tag = el.tagName.toLowerCase();
    if (tag === 'button') score += 10;
    if ((el.getAttribute('type') || '').toLowerCase() === 'submit') score += 15;
    if (pwd && pwd.form && el.form === pwd.form) score += 25;
    return score;
  }
  function findButton(pwd) {
    var nodes = Array.prototype.slice.call(document.querySelectorAll('button, input[type=submit], input[type=button], a, [role=button]'));
    var best = null;
    var bestScore = -1;
    for (var i = 0; i < nodes.length; i++) {
      var s = scoreButton(nodes[i], pwd);
      if (s > bestScore) { bestScore = s; best = nodes[i]; }
    }
    if (bestScore >= 45) return best;
    return null;
  }
  function submitForm(pwd, button) {
    if (state.submitted) return;
    state.submitted = true;
    if (button) {
      try { button.focus(); } catch (e) { }
      try {
        button.click();
        send('submitted', '已点击：' + ((button.innerText || button.value || '').replace(/\s+/g, ' ').trim()));
        return;
      } catch (e) { }
    }
    var form = pwd ? pwd.form : null;
    if (form) {
      try {
        if (form.requestSubmit) form.requestSubmit();
        else form.submit();
        send('submitted', '已提交登录表单');
        return;
      } catch (e) { }
    }
    if (pwd) {
      try {
        pwd.focus();
        var ev = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true };
        pwd.dispatchEvent(new KeyboardEvent('keydown', ev));
        pwd.dispatchEvent(new KeyboardEvent('keypress', ev));
        pwd.dispatchEvent(new KeyboardEvent('keyup', ev));
        send('submitted', '已尝试回车提交');
      } catch (e) { }
    }
  }
  function attempt() {
    if (state.finished) return;
    state.retries++;
    var fields = findFields();
    var button = findButton(fields.pwd);
    if (fields.pwd || button) {
      var filledUser = false, filledPwd = false;
      if (cfg.autoFill && fields.user && cfg.username) {
        setValue(fields.user, cfg.username); filledUser = true;
      }
      if (cfg.autoFill && fields.pwd && cfg.password) {
        setValue(fields.pwd, cfg.password); filledPwd = true;
      }
      send('filled', 'user=' + (fields.user ? 1 : 0) + ',pwd=' + (fields.pwd ? 1 : 0) +
        ',userFilled=' + (filledUser ? 1 : 0) + ',pwdFilled=' + (filledPwd ? 1 : 0) +
        ',button=' + (button ? ((button.innerText || button.value || '').replace(/\s+/g, ' ').trim().substring(0, 30)) : ''));
      if (cfg.autoSubmit) {
        if (hasCaptcha()) {
          state.finished = true;
          send('captcha', '页面包含验证码，已停止自动提交');
          return;
        }
        if (!fields.pwd && !fields.user && !button) {
          schedule();
          return;
        }
        state.finished = true;
        setTimeout(function () { submitForm(fields.pwd, button); }, 550);
        return;
      }
      state.finished = true;
      return;
    }
    var onlineReason = pageLooksOnline();
    if (onlineReason) {
      state.finished = true;
      state.pageOnlineSent = true;
      send('already', onlineReason);
      return;
    }
    if (Date.now() - state.started > 14000) {
      state.finished = true;
      send('noform', '未找到登录表单');
      return;
    }
    schedule();
  }
  function schedule() {
    if (state.finished) return;
    setTimeout(attempt, 500);
  }
  setTimeout(attempt, 350);
  var onlineTimer = setInterval(function () {
    if (!window.__quickPortalState || window.__quickPortalState.pageOnlineSent) return;
    var reason = pageLooksOnline();
    if (reason) {
      window.__quickPortalState.pageOnlineSent = true;
      send('pageonline', reason);
      clearInterval(onlineTimer);
    }
  }, 1500);
  setTimeout(function () { clearInterval(onlineTimer); }, 120000);
})();");
            return sb.ToString();
        }
    }
}
