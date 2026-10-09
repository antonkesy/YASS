import { describe, expect, it, vi } from 'vitest';
import { escape, layer, openLayers, typing } from '$lib/keys.svelte';

const key = (k: string, target?: HTMLElement) => {
	const e = new KeyboardEvent('keydown', { key: k, bubbles: true });
	if (target) Object.defineProperty(e, 'target', { value: target });
	return e;
};

describe('the Escape stack', () => {
	it('closes only the newest layer, then the one under it', () => {
		const under = vi.fn();
		const over = vi.fn();
		const a = layer(under);
		const b = layer(over);
		expect(b.top).toBe(true);
		expect(a.top).toBe(false);

		expect(escape(key('Escape'))).toBe(true);
		expect(over).toHaveBeenCalledOnce();
		expect(under).not.toHaveBeenCalled();

		b.pop();
		escape(key('Escape'));
		expect(under).toHaveBeenCalledOnce();
		a.pop();
		expect(openLayers()).toBe(0);
		expect(escape(key('Escape'))).toBe(false);
	});

	it('leaves a text field before closing anything', () => {
		const close = vi.fn();
		const l = layer(close);
		const input = document.createElement('input');
		document.body.append(input);
		input.focus();

		expect(escape(key('Escape', input))).toBe(true);
		expect(document.activeElement).not.toBe(input);
		expect(close).not.toHaveBeenCalled();
		l.pop();
		input.remove();
	});

	it('counts text fields as typing, but not checkboxes or buttons', () => {
		const make = (html: string) => {
			const host = document.createElement('div');
			host.innerHTML = html;
			return host.firstElementChild as HTMLElement;
		};
		expect(typing(key('a', make('<input type="text">')))).toBe(true);
		expect(typing(key('a', make('<textarea></textarea>')))).toBe(true);
		expect(typing(key('a', make('<input type="checkbox">')))).toBe(false);
		expect(typing(key('a', make('<button></button>')))).toBe(false);
	});
});
