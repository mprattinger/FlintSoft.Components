const editors = new WeakMap();

export function attach(textarea, reference) {
    if (editors.has(textarea)) return;
    const root = textarea.parentElement;
    let selection = [textarea.selectionStart, textarea.selectionEnd];
    let revision = 0;
    let lastValue = textarea.value;
    let pending = false;
    let disposed = false;
    const remember = () => { selection = [textarea.selectionStart, textarea.selectionEnd]; };
    const input = () => { revision++; lastValue = textarea.value; remember(); };
    const sync = () => {
        if (textarea.value !== lastValue) { revision++; lastValue = textarea.value; }
    };
    const click = event => {
        if (!(event.target instanceof Element)) return;
        const button = event.target.closest('button[data-fs-markdown-action]');
        if (button && root.contains(button) && !button.disabled) apply(textarea, button.dataset.fsMarkdownAction);
    };
    const keydown = event => {
        if (textarea.disabled || textarea.readOnly || event.isComposing) return;
        const start = textarea.selectionStart, end = textarea.selectionEnd;
        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key === ']') {
            event.preventDefault();
            if (start === end) textarea.setRangeText('\t', start, end, 'end');
            else {
                const lineStart = textarea.value.slice(0, start).lastIndexOf('\n') + 1;
                const lines = textarea.value.slice(lineStart, end).replace(/(^|\n)(?=[\s\S])/g, '$1\t');
                textarea.setRangeText(lines, lineStart, end, 'select');
            }
            changed(textarea);
            return;
        }
        if (event.shiftKey || event.ctrlKey || event.altKey || event.metaKey) return;
        if (event.key !== 'Enter' || start !== end || (end !== textarea.value.length && textarea.value[end] !== '\n')) return;
        const lineStart = textarea.value.slice(0, start).lastIndexOf('\n') + 1;
        const item = textarea.value.slice(lineStart, start).match(/^([ \t]*)(\*|\d{1,9}\.)[ \t]+(.*)$/);
        if (!item) return;
        event.preventDefault();
        if (!item[3].trim()) textarea.setRangeText('', lineStart, start, 'end');
        else textarea.setRangeText(`\n${item[1]}${item[2] === '*' ? '*' : `${Number.parseInt(item[2], 10) + 1}.`} `, start, end, 'end');
        changed(textarea);
    };
    const report = async message => {
        if (!disposed) await reference.invokeMethodAsync('ReportPasteErrorAsync', message);
    };
    const paste = event => {
        if (textarea.disabled || textarea.readOnly || textarea.dataset.fsPasteHandler !== 'true') return;
        const item = Array.from(event.clipboardData?.items ?? []).find(value => value.type.startsWith('image/'));
        if (!item) return;
        event.preventDefault();
        if (pending) { void report('Another image paste is still being processed.'); return; }
        const file = item.getAsFile();
        if (!file) { void report('The pasted image could not be read.'); return; }
        const start = textarea.selectionStart, end = textarea.selectionEnd;
        const original = textarea.value, version = revision;
        pending = true;
        void (async () => {
            let stream;
            try {
                await reference.invokeMethodAsync('ClearPasteErrorAsync');
                stream = DotNet.createJSStreamReference(file);
                const path = await reference.invokeMethodAsync('ProcessImagePasteAsync', stream, file.type, file.name || null);
                if (disposed) return;
                if (textarea.disabled || textarea.readOnly || textarea.value !== original || revision !== version)
                    throw new Error('The editor changed while the image was being processed. Paste the image again.');
                const url = encodeURI(path).replace(/%25(?=[0-9a-f]{2})/gi, '%')
                    .replace(/[()]/g, character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
                textarea.setRangeText(`![Image](${url})`, start, end, 'end');
                changed(textarea);
            } catch (error) {
                await report(String(error));
            } finally {
                if (stream) DotNet.disposeJSObjectReference(stream);
                pending = false;
            }
        })();
    };
    const observer = new MutationObserver(records => {
        if (!textarea.isConnected) detach(textarea);
        else if (records.some(record => record.type === 'attributes' && record.target === textarea)) revision++;
    });
    const cleanup = () => {
        disposed = true;
        textarea.removeEventListener('input', input);
        textarea.removeEventListener('select', remember);
        textarea.removeEventListener('keyup', remember);
        textarea.removeEventListener('pointerup', remember);
        textarea.removeEventListener('keydown', keydown);
        textarea.removeEventListener('paste', paste);
        root.removeEventListener('click', click);
        observer.disconnect();
        editors.delete(textarea);
    };
    textarea.addEventListener('input', input);
    textarea.addEventListener('select', remember);
    textarea.addEventListener('keyup', remember);
    textarea.addEventListener('pointerup', remember);
    textarea.addEventListener('keydown', keydown);
    textarea.addEventListener('paste', paste);
    root.addEventListener('click', click);
    observer.observe(document.body, { childList: true, subtree: true });
    observer.observe(textarea, { attributes: true, attributeFilter: ['disabled', 'readonly', 'data-fs-paste-handler'] });
    editors.set(textarea, { cleanup, sync, getSelection: () => selection });
}

export function detach(textarea) { editors.get(textarea)?.cleanup(); }
export function sync(textarea) { editors.get(textarea)?.sync(); }

function changed(textarea) {
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
}

export function apply(textarea, action) {
    const editor = editors.get(textarea);
    if (!editor) throw new Error('Markdown editor is not attached.');
    if (textarea.disabled || textarea.readOnly) return;
    const [savedStart, savedEnd] = editor.getSelection();
    const start = document.activeElement === textarea ? textarea.selectionStart : Math.min(savedStart, textarea.value.length);
    const end = document.activeElement === textarea ? textarea.selectionEnd : Math.min(savedEnd, textarea.value.length);
    const selected = textarea.value.slice(start, end);
    const previous = textarea.value;
    const inlineText = selected.trim();
    const inlineStart = start + selected.length - selected.trimStart().length;
    const inlineEnd = inlineStart + inlineText.length;
    const wrapInline = (before, after = before) => {
        if (start !== end && !inlineText) return;
        textarea.setRangeText(`${before}${inlineText}${after}`, inlineStart, inlineEnd, 'end');
    };
    switch (action) {
        case 'bold': wrapInline('**'); break;
        case 'italic': wrapInline('*'); break;
        case 'quote': textarea.setRangeText(`> ${selected}`, start, end, 'end'); break;
        case 'heading-1': case 'heading-2': case 'heading-3':
            textarea.setRangeText('#'.repeat(Number(action.at(-1))) + ' ', start, start, 'end'); break;
        case 'code': {
            const before = start > 0 && textarea.value[start - 1] !== '\n' ? '\n' : '';
            const after = end < textarea.value.length && textarea.value[end] !== '\n' ? '\n' : '';
            const closing = selected.endsWith('\n') ? '' : '\n';
            textarea.setRangeText(`${before}\`\`\`\n${selected}${closing}\`\`\`${after}`, start, end, 'end');
            if (start === end) textarea.setSelectionRange(start + before.length + 4, start + before.length + 4);
            break;
        }
        case 'link': {
            if (start !== end && !inlineText) break;
            const url = prompt('Enter URL:', 'https://');
            if (url) wrapInline('[', `](${url})`);
            break;
        }
        case 'list': case 'numbered-list': {
            const numbered = action === 'numbered-list';
            if (start === end) {
                const word = textarea.value.slice(0, start).match(/\S+$/);
                const atWordEnd = start === textarea.value.length || /\s/.test(textarea.value[start]);
                const point = word && atWordEnd ? start - word[0].length : start;
                textarea.setRangeText(numbered ? '1. ' : '* ', point, point, point < start ? 'preserve' : 'end');
            } else {
                const lineStart = textarea.value.slice(0, start).lastIndexOf('\n') + 1;
                let number = 1;
                const lines = textarea.value.slice(lineStart, end).replace(/(^|\n)(?=[\s\S])/g,
                    (_, prefix) => `${prefix}${numbered ? `${number++}. ` : '* '}`);
                textarea.setRangeText(lines, lineStart, end, 'end');
            }
            break;
        }
        default: throw new Error(`Unknown Markdown action: ${action}`);
    }
    textarea.focus();
    if (textarea.value !== previous) changed(textarea);
}
