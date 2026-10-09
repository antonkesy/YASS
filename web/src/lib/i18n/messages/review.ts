import type { Area } from '../types';

/** The review screen: the page on the stage, its toolbar, the strip, the overview and saving. */
export const review = {
	// The screen itself
	sessionTitle: { en: 'Session', de: 'Sitzung', fr: 'Session', es: 'Sesión' },
	sessionGone: {
		en: 'This session no longer exists.',
		de: 'Diese Sitzung gibt es nicht mehr.',
		fr: "Cette session n'existe plus.",
		es: 'Esta sesión ya no existe.'
	},
	loading: { en: 'Loading…', de: 'Lädt…', fr: 'Chargement…', es: 'Cargando…' },
	pages: { en: 'Pages', de: 'Seiten', fr: 'Pages', es: 'Páginas' },
	previousPage: {
		en: 'Previous page',
		de: 'Vorherige Seite',
		fr: 'Page précédente',
		es: 'Página anterior'
	},
	nextPage: { en: 'Next page', de: 'Nächste Seite', fr: 'Page suivante', es: 'Página siguiente' },
	pageN: { en: 'Page {n}', de: 'Seite {n}', fr: 'Page {n}', es: 'Página {n}' },
	loadingPage: {
		en: 'Loading page {n}',
		de: 'Seite {n} wird geladen',
		fr: 'Chargement de la page {n}',
		es: 'Cargando la página {n}'
	},
	waitingFirstPage: {
		en: 'Waiting for the first page…',
		de: 'Warte auf die erste Seite…',
		fr: 'En attente de la première page…',
		es: 'Esperando la primera página…'
	},
	noPages: { en: 'No pages', de: 'Keine Seiten', fr: 'Aucune page', es: 'Sin páginas' },

	// The sidebar
	headingName: { en: 'Name', de: 'Name', fr: 'Nom', es: 'Nombre' },
	headingSave: { en: 'Save', de: 'Speichern', fr: 'Enregistrer', es: 'Guardar' },
	headingScan: { en: 'Scan', de: 'Scan', fr: 'Scan', es: 'Escaneo' },
	headingMore: { en: 'More', de: 'Mehr', fr: 'Plus', es: 'Más' },
	size: { en: 'Size', de: 'Größe', fr: 'Taille', es: 'Tamaño' },
	pageSummary: {
		en: { one: '{active} of {n} page', other: '{active} of {n} pages' },
		de: { one: '{active} von {n} Seite', other: '{active} von {n} Seiten' },
		fr: { one: '{active} sur {n} page', other: '{active} sur {n} pages' },
		es: { one: '{active} de {n} página', other: '{active} de {n} páginas' }
	},
	scanMorePages: {
		en: 'Scan more pages',
		de: 'Weitere Seiten scannen',
		fr: 'Scanner plus de pages',
		es: 'Escanear más páginas'
	},
	addFromPastScan: {
		en: 'Add pages from past scan',
		de: 'Seiten aus früherem Scan hinzufügen',
		fr: "Ajouter des pages d'un ancien scan",
		es: 'Añadir páginas de un escaneo anterior'
	},
	noMergeable: {
		en: 'No other scan has pages to add',
		de: 'Kein anderer Scan hat Seiten zum Hinzufügen',
		fr: "Aucun autre scan n'a de pages à ajouter",
		es: 'Ningún otro escaneo tiene páginas para añadir'
	},
	discardScan: {
		en: 'Discard scan',
		de: 'Scan verwerfen',
		fr: 'Supprimer le scan',
		es: 'Descartar escaneo'
	},
	reallyDiscard: {
		en: 'Really discard?',
		de: 'Wirklich verwerfen?',
		fr: 'Vraiment supprimer ?',
		es: '¿Descartar de verdad?'
	},
	waitScan: {
		en: 'Wait for the scan to finish',
		de: 'Warte, bis der Scan fertig ist',
		fr: 'Attends la fin du scan',
		es: 'Espera a que termine el escaneo'
	},
	waitSave: {
		en: 'Wait for the save to finish',
		de: 'Warte, bis das Speichern fertig ist',
		fr: "Attends la fin de l'enregistrement",
		es: 'Espera a que termine el guardado'
	},
	notEditable: {
		en: 'This scan can no longer be edited',
		de: 'Dieser Scan kann nicht mehr bearbeitet werden',
		fr: 'Ce scan ne peut plus être modifié',
		es: 'Este escaneo ya no se puede editar'
	},
	documentName: {
		en: 'Document name',
		de: 'Dokumentname',
		fr: 'Nom du document',
		es: 'Nombre del documento'
	},

	// Saving
	preparing: { en: 'Preparing…', de: 'Wird vorbereitet…', fr: 'Préparation…', es: 'Preparando…' },
	exportProgress: {
		en: 'Export progress',
		de: 'Exportfortschritt',
		fr: "Progression de l'export",
		es: 'Progreso de la exportación'
	},
	stopScan: {
		en: 'Stop scanning',
		de: 'Scan abbrechen',
		fr: 'Arrêter la numérisation',
		es: 'Detener el escaneo'
	},
	stoppingScan: { en: 'Stopping…', de: 'Wird abgebrochen…', fr: 'Arrêt…', es: 'Deteniendo…' },
	scanStopped: {
		en: 'Scan stopped. The pages so far are kept.',
		de: 'Scan abgebrochen. Die bisherigen Seiten bleiben erhalten.',
		fr: 'Numérisation arrêtée. Les pages déjà numérisées sont conservées.',
		es: 'Escaneo detenido. Las páginas escaneadas se conservan.'
	},
	saveAs: {
		en: 'Save as…',
		de: 'Speichern unter…',
		fr: 'Enregistrer sous…',
		es: 'Guardar como…'
	},
	download: { en: 'Download', de: 'Herunterladen', fr: 'Télécharger', es: 'Descargar' },
	saveToServer: {
		en: 'Save to server',
		de: 'Auf dem Server speichern',
		fr: 'Enregistrer sur le serveur',
		es: 'Guardar en el servidor'
	},
	saveInto: {
		en: 'Save into {folder} on the server',
		de: 'In {folder} auf dem Server speichern',
		fr: 'Enregistrer dans {folder} sur le serveur',
		es: 'Guardar en {folder} en el servidor'
	},
	severalDocuments: {
		en: 'This scan is several documents. Pick a folder to save them into.',
		de: 'Dieser Scan besteht aus mehreren Dokumenten. Wähle einen Ordner dafür.',
		fr: 'Ce scan contient plusieurs documents. Choisis un dossier où les enregistrer.',
		es: 'Este escaneo tiene varios documentos. Elige una carpeta para guardarlos.'
	},
	fetchFailed: {
		en: 'The file could not be fetched ({status}).',
		de: 'Die Datei konnte nicht geladen werden ({status}).',
		fr: "Le fichier n'a pas pu être récupéré ({status}).",
		es: 'No se pudo obtener el archivo ({status}).'
	},

	// Toasts
	scanDiscarded: {
		en: 'Scan discarded',
		de: 'Scan verworfen',
		fr: 'Scan supprimé',
		es: 'Escaneo descartado'
	},
	savedFiles: {
		en: { one: 'Saved', other: 'Saved {n} files' },
		de: { one: 'Gespeichert', other: '{n} Dateien gespeichert' },
		fr: { one: 'Enregistré', other: '{n} fichiers enregistrés' },
		es: { one: 'Guardado', other: '{n} archivos guardados' }
	},
	downloadedFiles: {
		en: { one: 'Downloaded', other: 'Downloaded {n} files' },
		de: { one: 'Heruntergeladen', other: '{n} Dateien heruntergeladen' },
		fr: { one: 'Téléchargé', other: '{n} fichiers téléchargés' },
		es: { one: 'Descargado', other: '{n} archivos descargados' }
	},
	saveToFolder: {
		en: 'Save to folder',
		de: 'In Ordner speichern',
		fr: 'Enregistrer dans le dossier',
		es: 'Guardar en la carpeta'
	},
	savedToFolderAs: {
		en: 'Saved to folder as {file}',
		de: 'Im Ordner gespeichert als {file}',
		fr: 'Enregistré dans le dossier sous {file}',
		es: 'Guardado en la carpeta como {file}'
	},
	savedFilesToFolder: {
		en: 'Saved {n} files to folder',
		de: '{n} Dateien im Ordner gespeichert',
		fr: '{n} fichiers enregistrés dans le dossier',
		es: '{n} archivos guardados en la carpeta'
	},
	savedToServerAs: {
		en: 'Saved to server as {file}',
		de: 'Auf dem Server gespeichert als {file}',
		fr: 'Enregistré sur le serveur sous {file}',
		es: 'Guardado en el servidor como {file}'
	},
	savedFilesToServer: {
		en: 'Saved {n} files to server',
		de: '{n} Dateien auf dem Server gespeichert',
		fr: '{n} fichiers enregistrés sur le serveur',
		es: '{n} archivos guardados en el servidor'
	},
	pagesDeleted: {
		en: { one: '{n} page deleted', other: '{n} pages deleted' },
		de: { one: '{n} Seite gelöscht', other: '{n} Seiten gelöscht' },
		fr: { one: '{n} page supprimée', other: '{n} pages supprimées' },
		es: { one: '{n} página eliminada', other: '{n} páginas eliminadas' }
	},
	// English has always said "pages" here, whatever the count; the tests hold it to that.
	pagesAdded: {
		en: { one: '{n} page added', other: '{n} pages added' },
		de: { one: '{n} Seite hinzugefügt', other: '{n} Seiten hinzugefügt' },
		fr: { one: '{n} page ajoutée', other: '{n} pages ajoutées' },
		es: { one: '{n} página añadida', other: '{n} páginas añadidas' }
	},
	noBorders: {
		en: 'No borders found',
		de: 'Keine Ränder gefunden',
		fr: 'Aucune bordure trouvée',
		es: 'No se encontraron bordes'
	},
	croppedPages: {
		en: { one: 'Cropped {n} page', other: 'Cropped {n} pages' },
		de: { one: '{n} Seite zugeschnitten', other: '{n} Seiten zugeschnitten' },
		fr: { one: '{n} page recadrée', other: '{n} pages recadrées' },
		es: { one: '{n} página recortada', other: '{n} páginas recortadas' }
	},

	// The toolbar
	pageEdits: {
		en: 'Page edits',
		de: 'Seitenbearbeitung',
		fr: 'Retouches de page',
		es: 'Edición de páginas'
	},
	actingOnPage: {
		en: 'Acting on this page',
		de: 'Für diese Seite',
		fr: 'Sur cette page',
		es: 'En esta página'
	},
	actingOnSelected: {
		en: { one: 'Acting on {n} selected', other: 'Acting on {n} selected' },
		de: { one: 'Für {n} ausgewählte', other: 'Für {n} ausgewählte' },
		fr: { one: 'Sur {n} sélectionnée', other: 'Sur {n} sélectionnées' },
		es: { one: 'En {n} seleccionada', other: 'En {n} seleccionadas' }
	},
	clear: { en: 'Clear', de: 'Aufheben', fr: 'Effacer', es: 'Quitar' },
	allN: { en: 'All {n}', de: 'Alle {n}', fr: 'Toutes les {n}', es: 'Todas las {n}' },
	openFullScreen: {
		en: 'Open this page full screen',
		de: 'Diese Seite im Vollbild öffnen',
		fr: 'Ouvrir cette page en plein écran',
		es: 'Abrir esta página a pantalla completa'
	},
	rotateLeft: {
		en: 'Rotate left',
		de: 'Nach links drehen',
		fr: 'Tourner à gauche',
		es: 'Girar a la izquierda'
	},
	rotateRight: {
		en: 'Rotate right',
		de: 'Nach rechts drehen',
		fr: 'Tourner à droite',
		es: 'Girar a la derecha'
	},
	cropAndStraighten: {
		en: 'Crop and straighten',
		de: 'Zuschneiden und ausrichten',
		fr: 'Recadrer et redresser',
		es: 'Recortar y enderezar'
	},
	adjustLong: {
		en: 'Adjust brightness, contrast and sharpness',
		de: 'Helligkeit, Kontrast und Schärfe anpassen',
		fr: 'Ajuster luminosité, contraste et netteté',
		es: 'Ajustar brillo, contraste y nitidez'
	},
	filter: { en: 'Filter', de: 'Filter', fr: 'Filtre', es: 'Filtro' },
	rescanPage: {
		en: 'Rescan this page',
		de: 'Diese Seite neu scannen',
		fr: 'Rescanner cette page',
		es: 'Volver a escanear esta página'
	},
	restore: { en: 'Restore', de: 'Wiederherstellen', fr: 'Restaurer', es: 'Restaurar' },
	delete: { en: 'Delete', de: 'Löschen', fr: 'Supprimer', es: 'Eliminar' },
	closeCropMenu: {
		en: 'Close crop menu',
		de: 'Zuschneidemenü schließen',
		fr: 'Fermer le menu de recadrage',
		es: 'Cerrar el menú de recorte'
	},
	cropMenuItem: { en: 'Crop…', de: 'Zuschneiden…', fr: 'Recadrer…', es: 'Recortar…' },
	straightenMenuItem: {
		en: 'Straighten…',
		de: 'Ausrichten…',
		fr: 'Redresser…',
		es: 'Enderezar…'
	},
	findingBorders: {
		en: 'Finding borders…',
		de: 'Suche Ränder…',
		fr: 'Recherche des bordures…',
		es: 'Buscando bordes…'
	},
	autoCrop: {
		en: 'Auto-crop borders',
		de: 'Ränder automatisch zuschneiden',
		fr: 'Recadrage auto des bordures',
		es: 'Recorte automático de bordes'
	},
	trimThisPage: {
		en: 'Trim the dark frame around this page',
		de: 'Dunklen Rahmen um diese Seite entfernen',
		fr: 'Retirer le cadre sombre de cette page',
		es: 'Quitar el marco oscuro de esta página'
	},
	trimPages: {
		en: 'Trim the dark frame around {n} pages',
		de: 'Dunklen Rahmen um {n} Seiten entfernen',
		fr: 'Retirer le cadre sombre de {n} pages',
		es: 'Quitar el marco oscuro de {n} páginas'
	},

	// The filter menu and its options
	closeFilterMenu: {
		en: 'Close filter menu',
		de: 'Filtermenü schließen',
		fr: 'Fermer le menu des filtres',
		es: 'Cerrar el menú de filtros'
	},
	cleanUp: { en: 'Clean up', de: 'Bereinigen', fr: 'Nettoyage', es: 'Limpieza' },
	despeckle: { en: 'Despeckle', de: 'Entflecken', fr: 'Dépoussiérer', es: 'Quitar motas' },
	despeckleHint: {
		en: 'Remove dust and specks',
		de: 'Staub und Flecken entfernen',
		fr: 'Enlever poussière et taches',
		es: 'Quitar polvo y motas'
	},
	autoLevels: {
		en: 'Auto-levels',
		de: 'Auto-Tonwert',
		fr: 'Niveaux auto',
		es: 'Niveles auto'
	},
	autoLevelsHint: {
		en: 'Make faded scans black on white',
		de: 'Blasse Scans schwarz auf weiß',
		fr: 'Scans pâles en noir sur blanc',
		es: 'Escaneos pálidos en negro sobre blanco'
	},
	original: { en: 'Original', de: 'Original', fr: 'Original', es: 'Original' },
	enhance: { en: 'Enhance', de: 'Verbessern', fr: 'Améliorer', es: 'Mejorar' },
	grayscale: { en: 'Grayscale', de: 'Graustufen', fr: 'Niveaux de gris', es: 'Escala de grises' },
	blackWhite: {
		en: 'Black & white',
		de: 'Schwarzweiß',
		fr: 'Noir et blanc',
		es: 'Blanco y negro'
	},
	color: { en: 'Color', de: 'Farbe', fr: 'Couleur', es: 'Color' },

	// The filmstrip and its thumbnails
	allPages: { en: 'All pages', de: 'Alle Seiten', fr: 'Toutes les pages', es: 'Todas las páginas' },
	gridLabel: {
		en: 'Pages. Drag to reorder, or use Alt+Arrow keys.',
		de: 'Seiten. Zum Umsortieren ziehen oder Alt+Pfeiltasten nutzen.',
		fr: 'Pages. Fais glisser pour réorganiser, ou utilise Alt+flèches.',
		es: 'Páginas. Arrastra para reordenar o usa Alt+flechas.'
	},
	scanningNext: {
		en: 'Scanning the next page',
		de: 'Nächste Seite wird gescannt',
		fr: 'Scan de la page suivante',
		es: 'Escaneando la página siguiente'
	},
	scanMore: { en: 'Scan more', de: 'Mehr scannen', fr: 'Scanner plus', es: 'Escanear más' },
	showPage: {
		en: 'Show page {n}',
		de: 'Seite {n} anzeigen',
		fr: 'Afficher la page {n}',
		es: 'Mostrar la página {n}'
	},
	showPageDeleted: {
		en: 'Show page {n}, deleted',
		de: 'Seite {n} anzeigen, gelöscht',
		fr: 'Afficher la page {n}, supprimée',
		es: 'Mostrar la página {n}, eliminada'
	},
	selectPage: {
		en: 'Select page {n}',
		de: 'Seite {n} auswählen',
		fr: 'Sélectionner la page {n}',
		es: 'Seleccionar la página {n}'
	},
	openPage: {
		en: 'Open page {n}',
		de: 'Seite {n} öffnen',
		fr: 'Ouvrir la page {n}',
		es: 'Abrir la página {n}'
	},
	blank: { en: 'blank', de: 'leer', fr: 'vierge', es: 'en blanco' },
	deleted: { en: 'deleted', de: 'gelöscht', fr: 'supprimée', es: 'eliminada' },

	// The all-pages overview
	closeOverview: {
		en: 'Close overview',
		de: 'Übersicht schließen',
		fr: "Fermer la vue d'ensemble",
		es: 'Cerrar la vista general'
	},
	pageCount: {
		en: { one: '{n} page', other: '{n} pages' },
		de: { one: '{n} Seite', other: '{n} Seiten' },
		fr: { one: '{n} page', other: '{n} pages' },
		es: { one: '{n} página', other: '{n} páginas' }
	},
	nSelected: {
		en: { one: '{n} selected', other: '{n} selected' },
		de: { one: '{n} ausgewählt', other: '{n} ausgewählt' },
		fr: { one: '{n} sélectionnée', other: '{n} sélectionnées' },
		es: { one: '{n} seleccionada', other: '{n} seleccionadas' }
	},
	selectAll: {
		en: 'Select all',
		de: 'Alle auswählen',
		fr: 'Tout sélectionner',
		es: 'Seleccionar todo'
	},
	zoomOut: { en: 'Zoom out', de: 'Verkleinern', fr: 'Dézoomer', es: 'Alejar' },
	zoomIn: { en: 'Zoom in', de: 'Vergrößern', fr: 'Zoomer', es: 'Acercar' },
	overviewHint: {
		en: 'Drag a page to reorder it · tap to show it · tick to select',
		de: 'Seite ziehen zum Umsortieren · tippen zum Anzeigen · abhaken zum Auswählen',
		fr: "Fais glisser une page pour la déplacer · touche pour l'afficher · coche pour sélectionner",
		es: 'Arrastra una página para moverla · tócala para verla · márcala para seleccionarla'
	},

	// The full-screen preview
	pageOf: {
		en: 'Page {n} of {total}',
		de: 'Seite {n} von {total}',
		fr: 'Page {n} sur {total}',
		es: 'Página {n} de {total}'
	},
	closePreview: {
		en: 'Close preview',
		de: 'Vorschau schließen',
		fr: "Fermer l'aperçu",
		es: 'Cerrar la vista previa'
	},
	fitToScreen: {
		en: 'Fit page to screen',
		de: 'Seite einpassen',
		fr: "Ajuster à l'écran",
		es: 'Ajustar a la pantalla'
	},
	zoomFullWidth: {
		en: 'Zoom to full width',
		de: 'Auf volle Breite zoomen',
		fr: 'Zoomer sur toute la largeur',
		es: 'Ampliar a todo el ancho'
	},
	editThisPage: {
		en: 'Edit this page',
		de: 'Diese Seite bearbeiten',
		fr: 'Modifier cette page',
		es: 'Editar esta página'
	},
	adjust: { en: 'Adjust', de: 'Anpassen', fr: 'Ajuster', es: 'Ajustar' },
	crop: { en: 'Crop', de: 'Zuschneiden', fr: 'Recadrer', es: 'Recortar' },
	straighten: { en: 'Straighten', de: 'Ausrichten', fr: 'Redresser', es: 'Enderezar' },
	scanned: { en: 'Scanned', de: 'Gescannt', fr: 'Numérisé', es: 'Escaneado' },
	scanner: { en: 'Scanner', de: 'Scanner', fr: 'Scanner', es: 'Escáner' },
	scanSettings: { en: 'Settings', de: 'Einstellungen', fr: 'Réglages', es: 'Ajustes' },
	pdfProperties: {
		en: 'PDF properties',
		de: 'PDF-Eigenschaften',
		fr: 'Propriétés PDF',
		es: 'Propiedades del PDF'
	},
	pdfNone: { en: 'None', de: 'Keine', fr: 'Aucune', es: 'Ninguna' }
} satisfies Area;
