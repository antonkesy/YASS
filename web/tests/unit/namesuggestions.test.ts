import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import NameSuggestions from '$lib/components/NameSuggestions.svelte';
import { NameSource } from '$lib/hub/generated/Yass.Core.Naming';
import type { NameSuggestionsDto } from '$lib/hub/generated/Yass.Core.Hub';

const result = (over: Partial<NameSuggestionsDto> = {}): NameSuggestionsDto => ({
	Suggestions: [
		{ Name: 'Quarterly Report', Source: NameSource.Ai, Reason: 'by a model', Confidence: 0.9 },
		{ Name: '2026-09-29_scan', Source: NameSource.Template, Reason: 'template', Confidence: 0.5 }
	],
	CanRead: true,
	Local: true,
	...over
});

const open = (props: Partial<Record<string, unknown>> = {}) =>
	render(NameSuggestions, {
		state: 'ready',
		result: result(),
		onpick: vi.fn(),
		onretry: vi.fn(),
		onclose: vi.fn(),
		...props
	});

describe('NameSuggestions', () => {
	it('counts the pages it is reading, and only offers Read again once it is done', () => {
		const { unmount } = open({ state: 'reading', result: null, pages: 3 });
		expect(screen.getByTestId('name-suggestions')).toHaveTextContent('Reading text on 3 pages…');
		expect(screen.queryByTestId('suggest-retry')).toBeNull();
		unmount();

		open({ state: 'reading', result: null, pages: 1 });
		expect(screen.getByTestId('name-suggestions')).toHaveTextContent('Reading text on 1 page…');
	});

	it('names the phase a model in this browser adds', () => {
		open({ state: 'downloading', result: null });
		expect(screen.getByTestId('name-suggestions')).toHaveTextContent('Downloading the model…');
	});

	it('marks the row the document is already called', () => {
		open({ current: 'Quarterly Report' });
		const rows = screen.getAllByRole('button', { name: /Quarterly Report|2026/ });
		expect(rows[0].className).toContain('bg-accent-50');
		expect(rows[1].className).not.toContain('bg-accent-50');
	});

	it('shows a failure alongside whatever did arrive, not instead of it', () => {
		open({ error: 'Anthropic rejected the API key.' });
		expect(screen.getByTestId('suggest-error')).toHaveAttribute('role', 'alert');
		expect(screen.getByTestId('suggest-error')).toHaveTextContent('rejected the API key');
		// The template row is still there to pick: a broken model costs nothing that worked.
		expect(screen.getByRole('button', { name: /2026-09-29_scan/ })).toBeInTheDocument();
	});

	it('sends the whole story when the server cannot read and offers nothing', () => {
		open({ result: result({ Suggestions: [], CanRead: false }) });
		const empty = screen.getByTestId('name-suggestions');
		expect(empty).toHaveTextContent('does not read page text');
		expect(empty).toHaveTextContent('Settings');
	});

	it('says exactly what left, since it is the one line nobody can check', () => {
		const { unmount } = open();
		expect(screen.getByTestId('suggest-privacy')).toHaveTextContent('Nothing left it');
		unmount();

		const away = result({ Local: false });
		const { unmount: second } = open({ result: away, sent: 'text' });
		expect(screen.getByTestId('suggest-privacy')).toHaveTextContent('The text read from the page');
		second();

		// OCR off means the page itself went, and saying "the text" would be a lie.
		open({ result: away, sent: 'image' });
		expect(screen.getByTestId('suggest-privacy')).toHaveTextContent('The page image was sent');
	});

	it('hands back the name that was picked', async () => {
		const onpick = vi.fn();
		open({ onpick });
		await fireEvent.click(screen.getByRole('button', { name: /Quarterly Report/ }));
		expect(onpick).toHaveBeenCalledWith('Quarterly Report');
	});
});
