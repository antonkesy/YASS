import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import StatusPill from '$lib/components/StatusPill.svelte';
import Toolbar from '$lib/components/Toolbar.svelte';
import ExportBar from '$lib/components/ExportBar.svelte';
import PageThumb from '$lib/components/PageThumb.svelte';
import { ScannerState } from '$lib/hub/generated/Yawss.Core.Scanning';
import { ColorMode, SessionState } from '$lib/hub/generated/Yawss.Core.Domain';
import type { SessionDto } from '$lib/hub/generated/Yawss.Core.Hub';

const session = (state = SessionState.Review, deleted = 0): SessionDto => ({
	Id: 's1',
	CreatedAt: '2026-09-24T10:00:00Z',
	State: state,
	OptionsUsed: {},
	Pages: [0, 1, 2].map((Index) => ({
		Index,
		Rotation: 0,
		Deleted: Index < deleted,
		IsBlank: false,
		Hash: `h${Index}`,
		ThumbnailReady: true
	}))
});

describe('StatusPill', () => {
	it('shows scanner readiness when connected and offline otherwise', () => {
		const { rerender } = render(StatusPill, {
			status: { State: ScannerState.Ready, DeviceName: 'ADF', CheckedAt: '' },
			connection: 'connected'
		});
		expect(screen.getByRole('status')).toHaveTextContent('Ready');
		rerender({ status: null, connection: 'reconnecting' });
		expect(screen.getByRole('status')).toHaveTextContent('Offline');
	});
});

describe('Toolbar', () => {
	it('acts on all pages when nothing is selected and emits edits', async () => {
		const onrotate = vi.fn();
		const oncolor = vi.fn();
		const onclear = vi.fn();
		render(Toolbar, {
			selectedCount: 0,
			total: 4,
			anyDeleted: false,
			onrotate,
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			oncolor,
			onclear
		});
		expect(screen.getByTestId('toolbar-scope')).toHaveTextContent('all 4');
		await fireEvent.click(screen.getByRole('button', { name: 'Rotate right' }));
		expect(onrotate).toHaveBeenCalledWith(90);
		expect(screen.getByTestId('restore-button')).toBeDisabled();
		await fireEvent.change(screen.getByTestId('color-select'), {
			target: { value: String(ColorMode.Color) }
		});
		expect(oncolor).toHaveBeenCalledWith(ColorMode.Color);
	});

	it('shows the selection count and a clear button', async () => {
		const onclear = vi.fn();
		render(Toolbar, {
			selectedCount: 2,
			total: 4,
			anyDeleted: true,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			oncolor: vi.fn(),
			onclear
		});
		expect(screen.getByTestId('toolbar-scope')).toHaveTextContent('2 selected');
		await fireEvent.click(screen.getByRole('button', { name: 'Clear' }));
		expect(onclear).toHaveBeenCalled();
	});
});

describe('ExportBar', () => {
	it('enables save only in review with active pages and needs a second tap to discard', async () => {
		const onsave = vi.fn();
		const ondiscard = vi.fn();
		render(ExportBar, {
			session: session(),
			allowDownload: true,
			onsave,
			ondownload: vi.fn(),
			ondiscard
		});
		expect(screen.getByTestId('page-count')).toHaveTextContent('3 of 3 pages');
		await fireEvent.click(screen.getByTestId('save-button'));
		expect(onsave).toHaveBeenCalled();
		await fireEvent.click(screen.getByTestId('discard-button'));
		expect(ondiscard).not.toHaveBeenCalled();
		expect(screen.getByTestId('discard-button')).toHaveTextContent('Really discard?');
		await fireEvent.click(screen.getByTestId('discard-button'));
		expect(ondiscard).toHaveBeenCalled();
	});

	it('disables export while scanning, with everything deleted, or when downloads are off', () => {
		const { rerender } = render(ExportBar, {
			session: session(SessionState.Scanning),
			allowDownload: false,
			onsave: vi.fn(),
			ondownload: vi.fn(),
			ondiscard: vi.fn()
		});
		expect(screen.getByTestId('save-button')).toBeDisabled();
		expect(screen.queryByTestId('download-button')).toBeNull();
		rerender({
			session: session(SessionState.Review, 3),
			allowDownload: true,
			onsave: vi.fn(),
			ondownload: vi.fn(),
			ondiscard: vi.fn()
		});
		expect(screen.getByTestId('save-button')).toBeDisabled();
		expect(screen.getByTestId('download-button')).toBeDisabled();
	});

	it('shows progress while exporting and the saved message afterwards', () => {
		const { rerender } = render(ExportBar, {
			session: session(SessionState.Exporting),
			progress: 40,
			onsave: vi.fn(),
			ondownload: vi.fn(),
			ondiscard: vi.fn()
		});
		expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '40');
		rerender({
			session: session(SessionState.Done),
			savedPath: '/data/consume/x.pdf',
			onsave: vi.fn(),
			ondownload: vi.fn(),
			ondiscard: vi.fn()
		});
		expect(screen.getByTestId('saved-message')).toBeInTheDocument();
	});
});

describe('PageThumb', () => {
	it('toggles selection on tap, opens on the expand button, and badges blank/deleted pages', async () => {
		const ontoggle = vi.fn();
		const onopen = vi.fn();
		const { container } = render(PageThumb, {
			sessionId: 's1',
			page: {
				Index: 2,
				Rotation: 0,
				Deleted: true,
				IsBlank: true,
				Hash: 'abc',
				ThumbnailReady: true
			},
			ontoggle,
			onopen
		});
		expect(screen.getByTestId('blank-badge')).toBeInTheDocument();
		expect(screen.getByTestId('page-thumb')).toHaveAttribute('data-deleted', 'true');
		expect(container.querySelector('img')).toHaveAttribute(
			'src',
			expect.stringContaining('/img/s1/2/thumb?h=abc')
		);
		await fireEvent.click(screen.getByRole('button', { name: /^Page 3/ }));
		expect(ontoggle).toHaveBeenCalledWith(2);
		await fireEvent.click(screen.getByRole('button', { name: 'Open page 3' }));
		expect(onopen).toHaveBeenCalledWith(2);
	});

	it('shows a spinner until the thumbnail exists', () => {
		const { container } = render(PageThumb, {
			sessionId: 's1',
			page: {
				Index: 0,
				Rotation: 0,
				Deleted: false,
				IsBlank: false,
				Hash: 'h',
				ThumbnailReady: false
			},
			ontoggle: vi.fn(),
			onopen: vi.fn()
		});
		expect(container.querySelector('img')).toBeNull();
	});
});
