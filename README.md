<h1>🗺️ CS2 Kartenwechsel & Zeitsteuerung (kompatibel mit ABS_MapCycle)</h1>

<h2>📌 Übersicht</h2>
<p>
Nach der letzten Runde eines Matches zeigt CS2 ein <strong>„Runde gewonnen/verloren“</strong>-Panel an.<br>
Wie lange dieses sichtbar bleibt, wird über folgende Einstellung gesteuert:
</p>

<pre><code>mp_win_panel_display_time 10
</code></pre>

<p>
Sobald diese Zeit abläuft, wechselt CS2 automatisch zum <strong>Kartenabstimmungsbildschirm</strong>.<br>
Auch wenn <code>mp_endmatch_votenextmap 0</code> gesetzt ist, erscheint dieser Bildschirm weiterhin – jedoch werden <strong>alle Stimmen ignoriert</strong>.
</p>

<p>
Wenn du <strong>CS2_MapCycle</strong> verwendest, wird die CS2-Abstimmung vollständig ignoriert und das Plugin wählt automatisch die nächste Karte aus deiner Rotation.
</p>

<hr>

<h2>⏱️ Funktionsweise der Zeitsteuerung</h2>

<p>Nach Matchende bestimmen <strong>zwei Einstellungen</strong> die Dauer bis zum eigentlichen Kartenwechsel:</p>

<ul>
  <li><code>mp_win_panel_display_time</code></li>
  <li><code>mp_match_restart_delay</code></li>
</ul>

<h3>🔹 Fall 1 – <code>mp_win_panel_display_time > mp_match_restart_delay</code></h3>
<ul>
  <li>Das Gewinn-/Verlust-Panel bleibt die gesamte Zeit sichtbar.</li>
  <li>Der Abstimmungsbildschirm erscheint <strong>nicht</strong>.</li>
  <li><strong>Gesamtverzögerung:</strong> <code>mp_win_panel_display_time</code></li>
</ul>

<h3>🔹 Fall 2 – <code>mp_match_restart_delay > mp_win_panel_display_time</code></h3>
<ul>
  <li>Das Panel wird für <code>mp_win_panel_display_time</code> Sekunden angezeigt.</li>
  <li>Anschließend erscheint die Kartenabstimmung für die Restzeit.</li>
  <li><strong>Gesamtverzögerung:</strong> <code>mp_match_restart_delay</code></li>
</ul>

<hr>

<h2>⚠️ Wichtige Hinweise</h2>

<p>
Die Karte muss <strong>vor Ablauf der eingestellten Verzögerung</strong> gewechselt werden.<br>
Wenn <code>mp_endmatch_votenextmap 0</code> aktiv ist und die Zeit ausläuft, versucht das Spiel, zu einer <strong>leeren Karte</strong> zu wechseln.
</p>

<p>Dies kann dazu führen, dass der Server:</p>
<ul>
  <li>abstürzt</li>
  <li>oder nicht mehr reagiert</li>
</ul>

<p><em>Dies ist ein Fehler des Spiels, nicht des Plugins.</em></p>

<p>
Die Einstellung <code>mp_endmatch_votenextleveltime</code> <strong>verlängert keine Gesamtzeit</strong>.<br>
Sie bestimmt lediglich, wie lange man <strong>innerhalb der Restart-Zeit</strong> auf die Karten klicken kann.
</p>

<hr>

<h2>✅ Empfohlene Einstellungen (für CS2_MapCycle)</h2>

<pre><code>mp_win_panel_display_time 5        // Panel 5 Sekunden sichtbar
mp_match_restart_delay 0           // Sofortige Weiterleitung zur nächsten Karte
mp_endmatch_votenextmap 1          // Aktiv lassen, falls etwas schiefgeht
</code></pre>

<p>
Auch wenn die Abstimmung aktiv ist, ignoriert das Plugin sie vollständig.<br>
Die Aktivierung verhindert jedoch potenzielle Serverprobleme.
</p>

<p><strong>Hinweis:</strong> Wenn <code>mp_match_restart_delay 0</code> ist, erscheint <strong>kein Abstimmungsbildschirm</strong> – unabhängig von <code>mp_endmatch_votenextmap</code>.</p>

<hr>

<h2>💡 Tipp</h2>
<p>Wenn du eine „Nächste Karte“-Nachricht länger anzeigen möchtest, setze:</p>

<pre><code>mp_match_restart_delay 15–20
</code></pre>

<p>Damit bleibt die Anzeige ausreichend lange sichtbar.</p>

<hr>

<h2>📊 Zusammenfassung</h2>

<table>
  <thead>
    <tr>
      <th>Einstellung</th>
      <th>Bedeutung</th>
      <th>Empfehlung</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><code>mp_win_panel_display_time</code></td>
      <td>Dauer der Gewinn-/Verlust-Anzeige</td>
      <td>5</td>
    </tr>
    <tr>
      <td><code>mp_match_restart_delay</code></td>
      <td>Verzögerung vor Kartenwechsel</td>
      <td>0</td>
    </tr>
    <tr>
      <td><code>mp_endmatch_votenextmap</code></td>
      <td>Abstimmungsfunktion (vom Plugin ignoriert)</td>
      <td>1</td>
    </tr>
    <tr>
      <td><code>mp_endmatch_votenextleveltime</code></td>
      <td>Klickzeit auf Kartenkacheln</td>
      <td><em>Standard</em></td>
    </tr>
  </tbody>
</table>
