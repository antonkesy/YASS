import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { serverFolder } from '../../playwright.config';

// The server runs `--fake pages=3,blank=2,delay=400ms,devices=…` (see playwright.config.ts).

async function scanToReview(page: Page) {
	await page.goto('/');
	await expect(page.getByTestId('scan-button')).toBeEnabled();
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);
	// The scan is over once its edits are on offer: the toolbar carries them only while the
	// session is editable, which it is not while the feeder is still going. Filter rather
	// than Delete — Delete depends on whether the page on the stage is deleted, and a blank
	// one starts that way.
	await expect(page.getByTestId('filter-button')).toBeVisible({ timeout: 20_000 });
	await expect(page.getByTestId('page-thumb')).toHaveCount(3);
}

/**
 * A browser with no save dialog to offer: what Firefox and Safari are, and what any
 * box served over plain http on a LAN is, secure contexts being the API's price.
 */
async function withoutSavePicker(page: Page) {
	await page.addInitScript(() => {
		Reflect.deleteProperty(window, 'showSaveFilePicker');
		Reflect.deleteProperty(window, 'showDirectoryPicker');
	});
}

test('scan, review, edit, download and save', async ({ page }) => {
	await withoutSavePicker(page);
	await scanToReview(page);

	// Blank page is pre-marked deleted with a badge; bottom bar counts active pages.
	const thumbs = page.getByTestId('page-thumb');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	await expect(thumbs.nth(1).getByTestId('blank-badge')).toBeVisible();
	await expect(page.getByTestId('page-count')).toHaveText('2 of 3 pages');

	// The strip only shows pages; ticking them is the all-pages overview's job.
	await expect(thumbs.nth(0).getByTestId('page-select')).toHaveCount(0);
	const firstImg = thumbs.nth(0).locator('img');
	await expect(firstImg).toBeVisible();
	const before = await firstImg.getAttribute('src');

	// Tick page 1 over there and rotate from the toolbar that comes with it: the edit
	// acts on the selection only, and the hash changes after the round trip.
	await page.getByTestId('overview-open').click();
	const overview = page.getByTestId('page-overview');
	const all = overview.getByTestId('page-thumb');
	await all.nth(0).getByTestId('page-select').click();
	await expect(overview.getByTestId('toolbar-scope')).toHaveText(/1 selected/);
	await overview.getByRole('button', { name: 'Rotate right' }).click();
	await expect(firstImg).not.toHaveAttribute('src', before!);
	await expect(all.nth(2).locator('img')).toHaveAttribute('src', /h=[0-9a-f]{16}/);

	// Restore the blank page, then delete it again, without leaving the overview.
	await overview.getByRole('button', { name: 'Clear' }).click();
	await all.nth(1).getByTestId('page-select').click();
	await overview.getByTestId('restore-button').click();
	await expect(all.nth(1)).toHaveAttribute('data-deleted', 'false');
	await expect(page.getByTestId('page-count')).toHaveText('3 of 3 pages');
	await overview.getByTestId('delete-button').click();
	await expect(all.nth(1)).toHaveAttribute('data-deleted', 'true');

	// Select all takes the lot, and the toolbar's Clear is the only way back.
	await overview.getByRole('button', { name: 'Clear' }).click();
	await overview.getByTestId('overview-select-all').click();
	await expect(overview.getByTestId('toolbar-scope')).toHaveText(/3 selected/);
	await expect(overview.getByTestId('overview-select-all')).toBeDisabled();
	await overview.getByRole('button', { name: 'Clear' }).click();
	await page.getByTestId('overview-close').click();
	await expect(overview).toHaveCount(0);

	// Tapping the page itself only moves the stage; nothing joins the selection.
	await thumbs.nth(0).getByRole('button', { name: 'Show page 1' }).click();
	await expect(thumbs.nth(0)).toHaveAttribute('data-current', 'true');
	// The toolbar under it edits that page and says nothing about a selection: picking
	// several at once is the all-pages view's job, and it keeps its scope line over there.
	await expect(page.getByTestId('toolbar-scope')).toHaveCount(0);
	await expect(page.getByTestId('toolbar-select-all')).toHaveCount(0);
	// The strip shows pages; opening one full screen is the toolbar's Expand.
	await expect(thumbs.nth(0).getByRole('button', { name: 'Open page 1' })).toHaveCount(0);

	// Full-page preview of whatever the stage is showing, with next/prev and keyboard.
	await page.getByTestId('expand-button').click();
	const preview = page.getByTestId('preview');
	await expect(preview).toBeVisible();
	await expect(page.getByTestId('preview-counter')).toHaveText('1 / 3');

	// Zoom fills the viewport width instead of fitting the page; moving on resets it.
	const zoom = page.getByTestId('preview-zoom');
	const previewImg = preview.locator('img');
	const fitted = await previewImg.boundingBox();
	await zoom.click();
	await expect(zoom).toHaveAttribute('aria-pressed', 'true');
	const enlarged = await previewImg.boundingBox();
	expect(enlarged!.width).toBeGreaterThan(fitted!.width);

	await page.getByTestId('preview-next').click();
	await expect(zoom).toHaveAttribute('aria-pressed', 'false');
	await expect(page.getByTestId('preview-counter')).toHaveText(/2 \/ 3/);
	await page.keyboard.press('ArrowRight');
	await expect(page.getByTestId('preview-counter')).toHaveText('3 / 3');
	await page.keyboard.press('Escape');
	await expect(preview).toBeHidden();

	// Naming the scan is what the past scans list shows instead of the timestamp.
	await page.getByTestId('name-input').fill('Tax return 2025');
	await page.getByTestId('name-input').press('Enter');

	// With no save dialog to offer, the one button is a plain download of a real one-shot PDF.
	await expect(page.getByTestId('download-button')).toHaveText(/Download/);
	const download = page.waitForEvent('download');
	await page.getByTestId('download-button').click();
	const file = await download;
	expect(file.suggestedFilename()).toMatch(/\.pdf$/);

	// Handing the document over finishes the session.
	await expect(page.getByTestId('session-state')).toHaveText('Downloaded', { timeout: 20_000 });

	// Home lists it as downloaded with the active page count.
	await page.getByRole('link', { name: 'Back to home' }).click();
	const list = page.getByTestId('session-list');
	await expect(list.getByTestId('session-status').first()).toHaveText('Downloaded');
	await expect(list.getByTestId('session-tile').first()).toContainText('2 p');
	await expect(list.getByTestId('session-tile').first()).toContainText('Tax return 2025');
});

test('the toolbar rotates the page on the stage, and the whole scan only when asked', async ({
	page
}) => {
	await scanToReview(page);
	const thumbs = page.getByTestId('page-thumb');
	const first = thumbs.nth(0).locator('img');
	const third = thumbs.nth(2).locator('img');
	await expect(first).toBeVisible();
	await expect(third).toBeVisible();

	// Page 1 is on the stage, so Rotate right turns page 1 and leaves page 3 alone.
	const firstBefore = await first.getAttribute('src');
	const thirdBefore = await third.getAttribute('src');
	await page.getByRole('button', { name: 'Rotate right' }).click();
	await expect(first).not.toHaveAttribute('src', firstBefore!);
	await expect(third).toHaveAttribute('src', thirdBefore!);

	// Move the stage and the target moves with it.
	await thumbs.nth(2).getByRole('button', { name: 'Show page 3' }).click();
	await page.getByRole('button', { name: 'Rotate right' }).click();
	await expect(third).not.toHaveAttribute('src', thirdBefore!);

	// Editing more than one at a time means the all-pages view, and the ticks stay there:
	// back on the review screen the toolbar is acting on the stage again.
	await page.getByTestId('overview-open').click();
	const overview = page.getByTestId('page-overview');
	await overview.getByTestId('toolbar-select-all').click();
	await expect(overview.getByTestId('toolbar-scope')).toHaveText(/3 selected/);
	const firstNow = await first.getAttribute('src');
	await overview.getByRole('button', { name: 'Rotate left' }).click();
	await expect(first).not.toHaveAttribute('src', firstNow!);

	await page.getByTestId('overview-close').click();
	const firstAfterAll = await first.getAttribute('src');
	const thirdNow = await third.getAttribute('src');
	await page.getByRole('button', { name: 'Rotate left' }).click();
	await expect(third).not.toHaveAttribute('src', thirdNow!);
	// Page 3 is still the one on the stage, and page 1 is untouched: the ticks went with
	// the view that made them.
	await expect(first).toHaveAttribute('src', firstAfterAll!);
});

test('the all-pages overview zooms, reorders by drag and hands the order back', async ({
	page
}) => {
	await scanToReview(page);

	await page.getByTestId('overview-open').click();
	const overview = page.getByTestId('page-overview');
	await expect(overview).toBeVisible();
	const thumbs = overview.getByTestId('page-thumb');
	await expect(thumbs).toHaveCount(3);

	// A first open puts the whole scan on one screen: there is nothing to scroll to.
	const grid = overview.getByTestId('page-grid');
	await expect
		.poll(() => grid.evaluate((el) => el.scrollHeight - el.clientHeight))
		.toBeLessThanOrEqual(1);

	// Zoom is a step at a time, and the tiles actually change size with it.
	const fitted = (await thumbs.nth(0).boundingBox())!.width;
	await page.getByTestId('overview-zoom-out').click();
	await expect.poll(async () => (await thumbs.nth(0).boundingBox())!.width).toBeLessThan(fitted);
	const chosen = (await thumbs.nth(0).boundingBox())!.width;

	// And that is remembered: fitting is where it starts, not what it always does.
	await page.getByTestId('overview-close').click();
	await page.getByTestId('overview-open').click();
	// The same step, not the same pixel: a tile is a `1fr` of whatever the grid is wide,
	// which a scrollbar coming or going moves by a hair.
	await expect
		.poll(async () =>
			Math.abs((await overview.getByTestId('page-thumb').nth(0).boundingBox())!.width - chosen)
		)
		.toBeLessThan(4);

	// Back down to the smallest step, where the whole scan fits on a phone screen too.
	const zoomOut = page.getByTestId('overview-zoom-out');
	while (await zoomOut.isEnabled()) await zoomOut.click();

	// Drag the blank page, which starts deleted, to the front of the scan.
	await expect(thumbs.nth(0)).toHaveAttribute('data-deleted', 'false');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	const blank = (await thumbs.nth(1).boundingBox())!;
	const first = (await thumbs.nth(0).boundingBox())!;
	await page.mouse.move(blank.x + blank.width / 2, blank.y + blank.height / 2);
	await page.mouse.down();
	// A finger has to hold before it drags, so that a swipe still scrolls; a mouse does
	// not wait, and the pause costs it nothing.
	await page.waitForTimeout(350);
	await page.mouse.move(first.x + first.width / 2, first.y + first.height / 2, { steps: 12 });
	await page.mouse.up();

	await expect(thumbs.nth(0)).toHaveAttribute('data-deleted', 'true');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'false');

	// Closing leaves the filmstrip showing the order the overview settled on.
	await page.getByTestId('overview-close').click();
	await expect(overview).toHaveCount(0);
	await expect(page.getByTestId('page-thumb').nth(0)).toHaveAttribute('data-deleted', 'true');
});

test('every scan setting can be set, and is applied to the next scan', async ({ page }) => {
	await page.goto('/settings');
	const form = page.getByTestId('settings-form');
	const dpi = form.getByTestId('seg-dpi');
	await expect(dpi).toBeVisible();
	// Nothing in yass.yaml hides a control: the file supplies defaults, the sheet sets the rest.
	await expect(form.locator('input[name="brightness"]')).toBeVisible();
	await expect(form.locator('input[name="blank_page_threshold"]')).toBeVisible();
	await dpi.getByRole('radio', { name: '150', exact: true }).check();
	await expect(page.getByTestId('overrides-active')).toBeVisible();

	await page.goto('/settings');
	await expect(dpi.getByRole('radio', { name: '150', exact: true })).toBeChecked();
	await page.getByTestId('reset-settings').click();
	await expect(page.getByTestId('overrides-active')).toBeHidden();
	await expect(dpi.getByRole('radio', { name: 'Default', exact: true })).toBeChecked();
});

test('the scanner is chosen on the home screen, and the presets follow it', async ({ page }) => {
	await page.goto('/');
	const picker = page.getByTestId('device-pills');
	// No "Automatic": with nothing picked, the row lights the scanner the server is on.
	await expect(picker.getByRole('radio', { checked: true })).toHaveText('YASS Fake ADF');
	await picker.getByRole('radio', { name: 'Canon LiDE 400' }).click();

	// Saved with no "available for all scanners" tick, so it belongs to the Canon alone.
	await page.goto('/settings');
	await expect(page.getByTestId('device-picker')).toHaveCount(0);
	await page.getByTestId('seg-dpi').getByRole('radio', { name: '150', exact: true }).check();
	await page.getByTestId('preset-new').click();
	await page.getByTestId('preset-name').fill('Canon only');
	await expect(page.getByTestId('preset-shared')).not.toBeChecked();
	await page.getByTestId('preset-save').click();

	const stage = page.getByTestId('scan-stage');
	await page.goto('/');
	await expect(stage.getByRole('button', { name: 'Canon only' })).toBeVisible();
	await page.getByTestId('device-pills').getByRole('radio', { name: 'Epson DS-530' }).click();
	await expect(stage.getByRole('button', { name: 'Canon only' })).toHaveCount(0);
	// The built-in presets belong to no scanner, so they are still all there.
	await expect(stage.getByRole('button', { name: 'Document' })).toBeVisible();

	// Opened up to every scanner, it comes back on the Epson too.
	await page.goto('/settings');
	await page.getByTestId('preset-scope').check();
	await page.goto('/');
	await expect(stage.getByRole('button', { name: 'Canon only' })).toBeVisible();
});

test('a picked preset stays picked, and says so once a setting moves away from it', async ({
	page
}) => {
	await page.goto('/settings');
	const presets = page.getByTestId('presets');
	const pill = (name: string) => presets.getByRole('button', { name, exact: true });

	// Saved on top of Document, so it pins Document's settings and more: both match at once.
	await pill('Document').click();
	await page.getByTestId('preset-new').click();
	await page.getByTestId('preset-name').fill('Office');
	await page.getByTestId('preset-save').click();
	await expect(pill('Office')).toHaveAttribute('aria-pressed', 'true');

	// Switching back and forth lights whichever was pressed, not the first that matches.
	await pill('Document').click();
	await expect(pill('Document')).toHaveAttribute('aria-pressed', 'true');
	await expect(pill('Office')).toHaveAttribute('aria-pressed', 'false');
	await pill('Office').click();
	await expect(pill('Office')).toHaveAttribute('aria-pressed', 'true');

	// Changing a setting leaves Office named as edited rather than dropping it.
	await page.getByTestId('seg-dpi').getByRole('radio', { name: '600', exact: true }).check();
	await expect(page.getByTestId('preset-edited')).toContainText('Office');
	await page.getByTestId('preset-revert').click();
	await expect(pill('Office')).toHaveAttribute('aria-pressed', 'true');
	await expect(
		page.getByTestId('seg-dpi').getByRole('radio', { name: '300', exact: true })
	).toBeChecked();
});

test('discard removes the scan from past scans', async ({ page }) => {
	await scanToReview(page);
	const url = page.url();
	await page.getByTestId('discard-button').click();
	await expect(page.getByTestId('discard-button')).toHaveText('Really discard?');
	await page.getByTestId('discard-button').click();
	await expect(page).toHaveURL(/\/$/);

	// Discarding deletes the scan outright: no row is left behind.
	const id = url.slice(url.lastIndexOf('/') + 1);
	await expect(page.locator(`a[href$="/session/${id}"]`)).toHaveCount(0);
});

test('save as… writes the document where the save dialog points', async ({ page }) => {
	type Saved = { name: string | null; bytes: number };
	await page.addInitScript(() => {
		const saved: Saved = { name: null, bytes: 0 };
		Object.assign(window, {
			__saved: saved,
			// Stands in for the dialog: Playwright cannot drive a native one, but what the
			// page does with the handle it gets back is the part worth testing.
			showSaveFilePicker: (options: { suggestedName?: string }) => {
				saved.name = options.suggestedName ?? null;
				return Promise.resolve({
					createWritable: () =>
						Promise.resolve({
							write: (data: Blob) => {
								saved.bytes += data.size;
								return Promise.resolve();
							},
							close: () => Promise.resolve()
						})
				});
			},
			showDirectoryPicker: () => Promise.reject(new Error('one document needs no folder'))
		});
	});
	await scanToReview(page);
	await page.getByTestId('name-input').fill('Tax return 2025');
	await page.getByTestId('name-input').press('Enter');

	await expect(page.getByTestId('download-button')).toHaveText(/Save as…/);
	await page.getByTestId('download-button').click();

	const saved = () => page.evaluate(() => (window as unknown as { __saved: Saved }).__saved);
	// A two-page PDF of blank fake pages is small, but it is the whole document.
	await expect.poll(async () => (await saved()).bytes, { timeout: 20_000 }).toBeGreaterThan(500);
	expect((await saved()).name).toBe('Tax return 2025.pdf');
	// Handing the document over finishes the session.
	await expect(page.getByTestId('session-state')).toHaveText('Downloaded');
});

test('scan more appends pages', async ({ page }) => {
	await scanToReview(page);

	// The dashed tile at the end of the filmstrip runs the feeder again.
	await page.getByTestId('scan-more-tile').click();
	await expect(page.getByTestId('page-thumb')).toHaveCount(6, { timeout: 30_000 });
	// Over once the edits are on offer again, as in scanToReview above.
	await expect(page.getByTestId('filter-button')).toBeVisible({ timeout: 30_000 });
	// 4 of 6: page 2 of each run comes out blank and starts deleted.
	await expect(page.getByTestId('page-count')).toHaveText('4 of 6 pages');
});

test('the name a scan is shown under ends in the format that will be written', async ({ page }) => {
	// The suffix beside a name is a promise about the file, and JPG writes one file per page
	// rather than one PDF. Wiring rather than markup: the settings preview and the review
	// screen both have to read the chosen format instead of assuming the default.
	await page.goto('/settings');
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}');
	await expect(page.getByTestId('template-preview')).toContainText('.pdf');

	await page.getByTestId('export-section').getByRole('radio', { name: 'JPG' }).check();
	await expect(page.getByTestId('template-preview')).toContainText('.jpg');
	await expect(page.getByTestId('template-preview')).not.toContainText('.pdf');

	await scanToReview(page);
	await expect(page.getByTestId('document-name')).toContainText('.jpg');
});

test('the sidebar names its groups, and hides none of them', async ({ page }) => {
	await scanToReview(page);

	// Four groups, all of them open: nothing in this panel is behind a fold.
	const aside = page.locator('aside');
	for (const title of ['Name', 'Save', 'Scan', 'More']) {
		await expect(aside.getByText(title, { exact: true })).toBeVisible();
	}

	await expect(page.getByTestId('page-count')).toHaveText('2 of 3 pages');
	// The filter is the toolbar's to show, under the page; the sidebar does not repeat it.
	await expect(page.getByTestId('scan-section')).not.toContainText('Filter');
	await expect(page.getByTestId('scan-section')).toContainText('Size');
	await expect(page.getByTestId('scan-more-button')).toBeVisible();
	await expect(page.getByTestId('merge-button')).toBeVisible();
	// Discarding sits on its own at the foot, away from the routine actions above it.
	await expect(page.getByTestId('discard-button')).toBeVisible();
});

test('post-processing: adjust, straighten, clean up and auto-crop a page', async ({ page }) => {
	await scanToReview(page);
	const stage = page.getByRole('img', { name: 'Page 1', exact: true });
	const hashOf = async () =>
		new URL((await stage.getAttribute('src'))!, page.url()).searchParams.get('h');
	await expect(stage).toBeVisible();
	const original = await hashOf();

	// Adjust: the stage's image is re-rendered under a new hash once the edit lands.
	await page.getByTestId('adjust-button').click();
	const adjust = page.getByTestId('adjust-modal');
	await adjust.getByLabel('Brightness').fill('40');
	await expect(page.getByTestId('adjust-preview')).toHaveAttribute('style', /brightness\(1\.2\b/);
	await page.getByTestId('adjust-apply').click();
	await expect(adjust).toBeHidden();
	await expect.poll(hashOf).not.toBe(original);
	const adjusted = await hashOf();

	// Straighten, from the Crop menu.
	await page.getByTestId('crop-button').click();
	await page.getByTestId('straighten-open').click();
	await page.getByTestId('straighten-right').click();
	await page.getByTestId('straighten-right').click();
	await page.getByTestId('straighten-apply').click();
	await expect(page.getByTestId('straighten-modal')).toBeHidden();
	await expect.poll(hashOf).not.toBe(adjusted);
	const straightened = await hashOf();

	// A clean-up ticks on in the filter menu and stays ticked when it is opened again.
	await page.getByTestId('filter-button').click();
	await page.getByRole('menuitemcheckbox', { name: /Despeckle/ }).click();
	await expect(page.getByRole('menuitemcheckbox', { name: /Despeckle/ })).toHaveAttribute(
		'aria-checked',
		'true'
	);
	await page.getByRole('button', { name: 'Close filter menu' }).click({ position: { x: 5, y: 5 } });
	await expect.poll(hashOf).not.toBe(straightened);

	// The fake scanner's pages are clean renders with no frame round them (ADR-012).
	await page.getByTestId('crop-button').click();
	await page.getByTestId('autocrop').click();
	await expect(page.getByTestId('toast')).toContainText('No borders found');

	// The same tools are on the full-screen preview's own bar.
	await page.getByTestId('expand-button').click();
	await expect(page.getByTestId('preview-edit-bar')).toBeVisible();
	await page.getByTestId('preview-adjust').click();
	await expect(page.getByTestId('adjust-modal')).toBeVisible();
	await page.keyboard.press('Escape');
	await expect(page.getByTestId('adjust-modal')).toBeHidden();
	// Escape closed the dialog, not the preview underneath it.
	await expect(page.getByTestId('preview')).toBeVisible();
});

test('the crop dialog draws over the whole page, upright, even once it is cropped', async ({
	page
}) => {
	await scanToReview(page);
	await page.getByRole('button', { name: 'Rotate right' }).click();

	const openCrop = async () => {
		await page.getByTestId('crop-button').click();
		await page.getByTestId('crop-open').click();
		const modal = page.getByTestId('crop-modal');
		await expect(modal).toBeVisible();
		// The frame image, not the page's (cropped) preview.
		const img = modal.getByRole('img');
		await expect(img).toHaveAttribute('src', /\/frame\?h=/);
		await expect
			.poll(() => img.evaluate((el: HTMLImageElement) => el.naturalWidth))
			.toBeGreaterThan(0);
		return modal;
	};

	await openCrop();
	await page.getByTestId('crop-top').fill('12');
	await page.getByTestId('crop-apply').click();
	await expect(page.getByTestId('crop-modal')).toBeHidden();

	// Opened again on the cropped, turned page, the box is where it was drawn.
	await openCrop();
	await expect(page.getByTestId('crop-top')).toHaveValue('12');
	await expect(page.getByTestId('crop-left')).toHaveValue('0');
});

test('a configured server folder can be switched on in settings and saved into', async ({
	page
}) => {
	// Scans are named uniquely so a rerun never finds last time's file under the same name.
	const name = `server-save-${Date.now()}`;

	await page.goto('/settings');
	await expect(page.getByTestId('server-folder')).toHaveText(serverFolder);
	const toggle = page.getByTestId('toggle-server-save').locator('input');
	await expect(toggle).not.toBeChecked();

	// Off: the review screen offers the download alone.
	await scanToReview(page);
	await expect(page.getByTestId('server-save-button')).toHaveCount(0);

	await page.goto('/settings');
	await page.getByTestId('toggle-server-save').locator('input').check();
	await scanToReview(page);
	await page.getByTestId('name-input').fill(name);
	await page.getByTestId('name-input').press('Enter');
	await page.getByTestId('server-save-button').click();

	await expect(page.getByTestId('toast')).toContainText(`Saved to server as ${name}.pdf`);
	const file = join(serverFolder, `${name}.pdf`);
	expect(existsSync(file)).toBe(true);
	expect(readFileSync(file).subarray(0, 4).toString()).toBe('%PDF');
	// Nothing half-written is left beside it.
	expect(readdirSync(serverFolder).filter((f) => f.startsWith('._yass-'))).toEqual([]);
});

test('Save as preset on the home screen opens Settings ready to name it', async ({ page }) => {
	await page.goto('/');
	await page.getByTestId('scan-stage').getByRole('button', { name: 'Document' }).click();
	// Moved off every preset, the home screen offers to keep these settings.
	await page.goto('/settings');
	await page.getByTestId('seg-dpi').getByRole('radio', { name: '150', exact: true }).check();
	await page.goto('/');
	await page.getByTestId('preset-save-current').click();

	await expect(page).toHaveURL(/\/settings$/);
	await expect(page.getByTestId('preset-name')).toBeFocused();
	await page.keyboard.type('Quick 150');
	await page.keyboard.press('Enter');

	await page.goto('/');
	await expect(
		page.getByTestId('scan-stage').getByRole('button', { name: 'Quick 150' })
	).toHaveAttribute('aria-pressed', 'true');
	await expect(page.getByTestId('preset-save-current')).toHaveCount(0);
});

test('the scanner pill sits centred over the page on the review screen', async ({ page }) => {
	await scanToReview(page);
	const pill = page.getByTestId('header-centre').locator('> *').first();
	const stage = page.getByRole('img', { name: 'Page 1', exact: true });
	await expect(stage).toBeVisible();

	const centre = async (l: typeof pill) => {
		const box = (await l.boundingBox())!;
		return box.x + box.width / 2;
	};
	expect(Math.abs((await centre(pill)) - (await centre(stage)))).toBeLessThan(3);
});

test('screens slide forward and back, and the settings sheet cross-fades', async ({ page }) => {
	// Records each view transition as it starts, with the direction the layout chose for it.
	await page.addInitScript(() => {
		const log: string[] = [];
		(window as unknown as { transitions: string[] }).transitions = log;
		const start = document.startViewTransition?.bind(document);
		if (start)
			document.startViewTransition = ((update: () => Promise<void>) => {
				log.push(document.documentElement.dataset.nav ?? '');
				return start(update);
			}) as typeof document.startViewTransition;
	});
	const transitions = () =>
		page.evaluate(() => (window as unknown as { transitions: string[] }).transitions);

	await scanToReview(page);
	// The transition slides the screen in, so the screen adds no slide of its own on top.
	await expect(page.locator('[class*="animate-slide-from"]')).toHaveCount(0);
	await page.getByLabel('Back to home').click();
	await expect(page.getByTestId('scan-button')).toBeVisible();
	await expect(page.locator('[class*="animate-slide-from"]')).toHaveCount(0);
	await page.getByRole('button', { name: /Change settings/ }).click();
	await expect(page).toHaveURL(/\/settings$/);

	await expect.poll(transitions).toEqual(['forward', 'back', 'fade']);
});

test('scanners can be renamed, hidden and inspected, and an unplugged one stays listed', async ({
	page
}) => {
	// A scanner this browser saw once and that is not on the server's list now.
	await page.addInitScript(() => {
		if (localStorage.getItem('yass.scanners')) return;
		localStorage.setItem(
			'yass.scanners',
			JSON.stringify({
				known: {
					'gone:1': {
						name: 'Old Fujitsu',
						driver: 'sane',
						firstSeen: '2026-01-01T10:00:00Z',
						lastSeen: '2026-02-01T10:00:00Z'
					}
				},
				labels: {},
				hidden: []
			})
		);
	});
	await page.goto('/settings');
	const list = page.getByTestId('scanner-list');
	const row = (name: string) =>
		list.getByTestId('scanner-row').filter({ has: page.getByText(name, { exact: true }) });

	await expect(row('Canon LiDE 400')).toHaveAttribute('data-connected', 'true');
	await expect(row('Old Fujitsu')).toHaveAttribute('data-connected', 'false');

	// Rename the Canon.
	await row('Canon LiDE 400').getByTestId('scanner-rename').click();
	await page.getByTestId('scanner-name-input').fill('Photo scanner');
	await page.getByTestId('scanner-name-input').press('Enter');
	await expect(row('Photo scanner')).toContainText('Canon LiDE 400');

	// Hide the Epson; its details say what the driver reported.
	await row('Epson DS-530').getByTestId('scanner-hide').click();
	await row('Epson DS-530').getByTestId('scanner-details-toggle').click();
	await expect(row('Epson DS-530').getByTestId('scanner-facts')).toContainText('fake');

	// The ghost can be forgotten.
	await row('Old Fujitsu').getByTestId('scanner-details-toggle').click();
	await row('Old Fujitsu').getByTestId('scanner-forget').click();
	await expect(row('Old Fujitsu')).toHaveCount(0);

	// The home screen's picker follows: the new name, and no Epson.
	await page.goto('/');
	const picker = page.getByTestId('device-pills');
	await expect(picker.getByRole('radio', { name: 'Photo scanner' })).toBeVisible();
	await expect(picker.getByRole('radio', { name: 'Epson DS-530' })).toHaveCount(0);
});

test('the header holds the language, the documentation and issue reporting', async ({ page }) => {
	await page.goto('/');
	// After a whole run Past scans holds long names; none may widen the page past the screen.
	await expect(page.getByTestId('past-scans')).toBeVisible();
	expect(
		await page.evaluate(
			() => document.documentElement.scrollWidth - document.documentElement.clientWidth
		)
	).toBeLessThanOrEqual(0);

	// Report an issue opens a menu of the issue forms, each going straight to its own.
	await page.getByTestId('header-issue').click();
	const forms = {
		bug: 'bug_report.yml',
		feature: 'feature_request.yml',
		scanner: 'scanner_support.yml'
	};
	for (const [id, file] of Object.entries(forms)) {
		const item = page.getByTestId(`issue-${id}`);
		await expect(item).toHaveAttribute(
			'href',
			`https://github.com/antonkesy/YASS/issues/new?template=${file}`
		);
		await expect(item).toHaveAttribute('target', '_blank');
	}
	await page.keyboard.press('Escape');
	await expect(page.getByTestId('issue-menu')).toHaveCount(0);

	// The documentation is a page of the app's own, opened in the same tab.
	await expect(page.getByTestId('header-docs')).not.toHaveAttribute('target');
	await page.getByTestId('header-docs').click();
	await expect(page).toHaveURL(/\/docs$/);
	await expect(page.getByTestId('docs-quote')).toHaveText(
		'You thought this &#@! would be documented?!?'
	);
	// The cat laughs: its frames change on their own.
	const cat = page.getByTestId('docs-cat');
	await expect(cat).toHaveAttribute('data-frame', '0');
	await expect(cat).toHaveAttribute('data-frame', '1');

	// The language menu switches the whole UI, and the choice outlives a reload.
	await page.getByTestId('header-language').click();
	await page.getByTestId('language-de').click();
	await expect(page.getByTestId('docs-quote')).toHaveText(
		'Du dachtest, dieser &#@! wäre dokumentiert?!?'
	);
	await expect(page.locator('html')).toHaveAttribute('lang', 'de');
	await page.reload();
	await expect(page.getByTestId('docs-quote')).toContainText('dokumentiert');
	await page.getByTestId('header-language').click();
	await page.getByTestId('language-en').click();
	await expect(page.getByTestId('docs-quote')).toContainText('documented');
});

test('the keyboard: pages, edits, full screen, shortcuts and Escape layer by layer', async ({
	page,
	isMobile
}) => {
	test.skip(isMobile, 'a phone has no keyboard');
	await scanToReview(page);
	const stage = (n: number) => page.getByRole('img', { name: `Page ${n}`, exact: true });
	await expect(stage(1)).toBeVisible();

	// Arrows step through the pages on the stage.
	await page.keyboard.press('ArrowRight');
	await expect(stage(2)).toBeVisible();
	await page.keyboard.press('ArrowLeft');
	await expect(stage(1)).toBeVisible();

	// R rotates the page in view: it comes back under a new hash.
	const before = await stage(1).getAttribute('src');
	await page.keyboard.press('r');
	await expect.poll(() => stage(1).getAttribute('src')).not.toBe(before);

	// Enter opens it full screen, where the arrows step on; Escape closes only the preview.
	await page.keyboard.press('Enter');
	await expect(page.getByTestId('preview')).toBeVisible();
	await page.keyboard.press('ArrowRight');
	await expect(page.getByTestId('preview-counter')).toContainText('2 /');
	// A dialog opened over it takes the first Escape for itself.
	await page.getByTestId('preview-adjust').click();
	await expect(page.getByTestId('adjust-modal')).toBeVisible();
	await page.keyboard.press('Escape');
	await expect(page.getByTestId('adjust-modal')).toBeHidden();
	await expect(page.getByTestId('preview')).toBeVisible();
	await page.keyboard.press('Escape');
	await expect(page.getByTestId('preview')).toBeHidden();
	await expect(page).toHaveURL(/\/session\//);

	// ? lists the shortcuts; Escape closes the list, not the screen.
	await page.keyboard.press('?');
	await expect(page.getByTestId('keyboard-help')).toBeVisible();
	await page.keyboard.press('Escape');
	await expect(page.getByTestId('keyboard-help')).toBeHidden();
	await expect(page).toHaveURL(/\/session\//);

	// Typing in the name field is typing: no shortcut fires, Escape only leaves the field.
	await page.getByTestId('name-input').click();
	await page.keyboard.type('ra');
	await expect(page.getByTestId('keyboard-help')).toHaveCount(0);
	await page.keyboard.press('Escape');
	await expect(page).toHaveURL(/\/session\//);

	// With nothing open, Escape goes back home; from Settings and the docs too.
	await page.keyboard.press('Escape');
	await expect(page.getByTestId('scan-button')).toBeVisible();
	await page.getByRole('button', { name: /Change settings/ }).click();
	await expect(page).toHaveURL(/\/settings$/);
	// Escape belongs to the sheet once it is there; the URL changes a beat before it mounts.
	await expect(page.getByRole('dialog', { name: 'Settings' })).toBeVisible();
	await page.keyboard.press('Escape');
	await expect(page).toHaveURL(/\/$/);
	await page.getByTestId('header-docs').click();
	await expect(page.getByTestId('docs-cat')).toBeVisible();
	await page.keyboard.press('Escape');
	await expect(page).toHaveURL(/\/$/);
});

test('switching presets on the home screen moves the Scan button smoothly, never in a jump', async ({
	page
}) => {
	await page.goto('/');
	const scan = page.getByTestId('scan-button');
	await expect(scan).toBeEnabled();
	const stage = page.getByTestId('scan-stage');
	const settled = () =>
		page.waitForFunction(() =>
			document
				.getAnimations()
				.every(
					(a) => a.playState !== 'running' || a.effect?.getComputedTiming().iterations === Infinity
				)
		);

	// The presets carry different settings — Receipt adds brightness and contrast chips,
	// Photo drops the feeder — so the chips can wrap to more or fewer rows. The area follows
	// them, and the Scan button under it may move, but only a little each frame.
	await stage.getByRole('button', { name: 'Document' }).click();
	await expect(stage.getByRole('button', { name: 'Document' })).toHaveAttribute(
		'aria-pressed',
		'true'
	);
	await settled();
	for (const name of ['Receipt', 'Photo', 'Double-sided', 'Document']) {
		// Follow the button's top every frame from just before the click until it has settled.
		await page.evaluate(() => {
			const el = document.querySelector('[data-testid="scan-button"]')!;
			const ys: number[] = [];
			(window as unknown as { ys: number[] }).ys = ys;
			const end = performance.now() + 700;
			const tick = () => {
				ys.push(el.getBoundingClientRect().top);
				if (performance.now() < end) requestAnimationFrame(tick);
			};
			tick();
		});
		await stage.getByRole('button', { name }).click();
		await expect(stage.getByRole('button', { name })).toHaveAttribute('aria-pressed', 'true');
		await page.waitForTimeout(750);
		const ys = await page.evaluate(() => (window as unknown as { ys: number[] }).ys);
		const total = Math.abs(ys[ys.length - 1] - ys[0]);
		const steps = ys.slice(1).map((y, i) => Math.abs(y - ys[i]));
		// Straight there, never past it and back...
		expect(
			steps.reduce((a, b) => a + b, 0),
			`after ${name}`
		).toBeLessThanOrEqual(total + 1);
		// ...and over several frames: a jump would cover the whole way in one.
		if (total > 4) expect(Math.max(...steps), `after ${name}`).toBeLessThan(total - 1);
	}

	// The chips sit right on top of the Scan button: no gap is left for rows that are not there.
	await settled();
	const area = (await page.getByTestId('settings-chips-area').boundingBox())!;
	const row = (await page.getByTestId('settings-chips').boundingBox())!;
	expect(Math.abs(area.height - row.height)).toBeLessThanOrEqual(1);

	// Off every preset, the Save as preset pill appears in room already kept for it: the same
	// chips, one value changed, leave the Scan button where it was.
	const start = Math.round((await scan.boundingBox())!.y);
	await page.goto('/settings');
	await page.getByTestId('seg-dpi').getByRole('radio', { name: '150', exact: true }).check();
	await page.goto('/');
	await expect(page.getByTestId('preset-save-current')).toBeVisible();
	await settled();
	expect(Math.round((await scan.boundingBox())!.y)).toBe(start);
});

test('an arrow with no page to go to is not shown, and the page does not move', async ({
	page
}) => {
	await scanToReview(page);
	const prev = page.getByTestId('stage-prev');
	const next = page.getByTestId('stage-next');
	await expect(page.getByRole('img', { name: 'Page 1', exact: true })).toBeVisible();
	// A hidden arrow keeps its box, so the two arrows framing the page never move: measured on
	// them rather than on the page, which slides in on every change.
	const frame = async () => [
		Math.round((await prev.boundingBox())!.x),
		Math.round((await next.boundingBox())!.x)
	];

	// First page: nothing before it.
	await expect(prev).toBeHidden();
	await expect(next).toBeVisible();
	const first = await frame();

	// Last page: nothing after it, and the arrows — and the page between them — sit as before.
	await next.click();
	await next.click();
	await expect(page.getByRole('img', { name: 'Page 3', exact: true })).toBeVisible();
	await expect(next).toBeHidden();
	await expect(prev).toBeVisible();
	expect(await frame()).toEqual(first);
});

test('rotating keeps the page on screen, turned, until the new render is in', async ({ page }) => {
	await scanToReview(page);
	const stage = page.getByRole('img', { name: 'Page 1', exact: true });
	await expect(stage).toBeVisible();
	await expect(page.getByTestId('stage-loading')).toHaveCount(0);
	const before = await stage.getAttribute('src');

	// Watch from the click on: no spinner may come up in front of the page at any point.
	await page.evaluate(() => {
		const w = window as unknown as { sawSpinner: boolean };
		w.sawSpinner = false;
		new MutationObserver(() => {
			if (document.querySelector('[data-testid=stage-loading]')) w.sawSpinner = true;
		}).observe(document.body, { childList: true, subtree: true });
	});
	await page.getByRole('button', { name: 'Rotate right' }).click();

	await expect.poll(() => stage.getAttribute('src')).not.toBe(before);
	// Once the new render is in, it is upright as rendered: no CSS turn left on it.
	await expect.poll(() => stage.evaluate((el) => (el as HTMLElement).style.transform)).toBe('');
	expect(await page.evaluate(() => (window as unknown as { sawSpinner: boolean }).sawSpinner)).toBe(
		false
	);
});

test("the save card shows the scan, picks the format and sets this scan's PDF properties", async ({
	page
}) => {
	await withoutSavePicker(page);
	await scanToReview(page);

	// What the scan is: when, on what, with which settings.
	const facts = page.getByTestId('scan-section');
	await expect(facts).toContainText('Scanned');
	await expect(facts).toContainText('Scanner');
	await expect(facts).toContainText('300 dpi');

	// The format right here, the same choice Settings holds; PDF properties only for a PDF.
	const format = page.getByTestId('seg-review-format');
	await format.getByRole('radio', { name: 'JPG' }).check();
	await expect(page.getByTestId('document-name')).toContainText('.jpg');
	await expect(page.getByTestId('pdf-properties')).toHaveCount(0);
	await format.getByRole('radio', { name: 'PDF' }).check();

	// This scan's own author, and it lands in the file.
	await page.getByTestId('pdf-properties-toggle').click();
	await page.getByTestId('pdf-author').fill('Jane Doe');
	await expect(page.getByTestId('pdf-properties-toggle')).toContainText('Jane Doe');
	const download = page.waitForEvent('download');
	await page.getByTestId('download-button').click();
	const file = await (await download).path();
	expect(readFileSync(file).toString('latin1')).toContain('/Author (Jane Doe)');

	// Settings followed the format switch, and kept its own (empty) author.
	await page.goto('/settings');
	await expect(
		page.getByTestId('export-section').getByRole('radio', { name: 'PDF' })
	).toBeChecked();
	await expect(page.getByTestId('metadata-author')).toHaveValue('');
});
