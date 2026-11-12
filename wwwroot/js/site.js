document.querySelectorAll(".manual-genre-pill input").forEach((checkbox) => {
  checkbox.addEventListener("change", () => {
    checkbox.closest(".genre-pill")?.classList.toggle("checked", checkbox.checked);
  });
});

const cameraGenreFilters = document.getElementById("camera-genre-filters");
document.querySelectorAll(".camera-genre-pill input").forEach((checkbox) => {
  checkbox.addEventListener("change", () => {
    checkbox.closest(".genre-pill")?.classList.toggle("checked", checkbox.checked);
    if (!cameraGenreFilters) {
      return;
    }

    const selected = [...document.querySelectorAll(".camera-genre-pill input:checked")].map((item) => item.value);
    cameraGenreFilters.value = selected.join(",");
  });
});

const emotionInput = document.getElementById("detected-emotion-input");
const emotionLabel = document.getElementById("detected-emotion-label");
const sourceInput = document.getElementById("camera-source-input");
const sourceLabel = document.getElementById("detected-emotion-source");
const cameraForm = document.getElementById("camera-form");
const manualRecommendForm = document.getElementById("manual-recommend-form");
const manualEmotionSelect = document.getElementById("emotion");
const resultsTitle = document.getElementById("results-title");
const resultsEmptyState = document.getElementById("results-empty-state");
const movieGridRoot = document.getElementById("movie-grid-root");
const recentSessionsRoot = document.getElementById("recent-sessions-root");
const heroDetectedMood = document.getElementById("hero-detected-mood");

function setEmotion(emotion, sourceText) {
  if (!emotionInput || !emotionLabel || !sourceInput) {
    return;
  }

  emotionInput.value = emotion;
  emotionLabel.textContent = emotion;
  sourceInput.value = sourceText;
  if (sourceLabel) {
    sourceLabel.textContent = sourceText;
  }

  if (heroDetectedMood) {
    heroDetectedMood.textContent = emotion;
  }

  if (manualEmotionSelect) {
    manualEmotionSelect.value = emotion;
  }
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function renderRecommendations(payload) {
  if (!resultsTitle || !movieGridRoot || !resultsEmptyState) {
    return;
  }

  resultsTitle.textContent = `${payload.movieCount} recommendations for ${payload.selectedEmotion}`;

  if (payload.emptyStateMessage) {
    resultsEmptyState.hidden = false;
    resultsEmptyState.textContent = payload.emptyStateMessage;
  } else {
    resultsEmptyState.hidden = true;
    resultsEmptyState.textContent = "";
  }

  movieGridRoot.innerHTML = (payload.recommendations || []).map((item) => `
    <article class="movie-card">
      <div class="movie-poster" style="background-image: url('${escapeHtml(item.posterUrl)}');"></div>
      <div class="movie-body">
        <div class="movie-topline">
          <span>${escapeHtml(item.year)}</span>
          <span>${escapeHtml(item.runtimeMinutes)} min</span>
          <span>${escapeHtml(item.rating)}/10</span>
        </div>
        <h3>${escapeHtml(item.title)}</h3>
        <p class="genres">${escapeHtml(item.genres)}</p>
        <p class="overview">${escapeHtml(item.overview)}</p>
        <p class="reason">${escapeHtml(item.reason)}</p>
        <div class="movie-footer">
          <span class="score-badge">Match ${escapeHtml(item.score)}</span>
          <span class="streaming-hint">${escapeHtml(item.streamingHint)}</span>
        </div>
      </div>
    </article>`).join("");
}

function renderRecentSessions(payload) {
  if (!recentSessionsRoot) {
    return;
  }

  const sessions = payload.recentSessions || [];
  if (!sessions.length) {
    recentSessionsRoot.innerHTML = `<p class="section-note">No saved sessions yet. Use the automatic detector or manual mood flow to start building your library.</p>`;
    return;
  }

  recentSessionsRoot.innerHTML = sessions.map((session) => `
    <article class="session-card">
      <div class="session-topline">
        <strong>${escapeHtml(session.detectedEmotion)}</strong>
        <span>${escapeHtml(session.createdUtc)}</span>
      </div>
      <p>${escapeHtml(session.emotionSource)}</p>
      <small>Filters: ${escapeHtml(session.genreFilters)}</small>
      <small>Recommended: ${escapeHtml(session.recommendedTitles)}</small>
    </article>`).join("");
}

function syncCameraGenreFilters() {
  if (!cameraGenreFilters) {
    return;
  }

  const selected = [...document.querySelectorAll(".camera-genre-pill input:checked")].map((item) => item.value);
  cameraGenreFilters.value = selected.join(",");
}

async function postFormInPlace(form, onComplete) {
  const response = await fetch(form.action, {
    method: "POST",
    headers: { "X-Requested-With": "XMLHttpRequest" },
    body: new FormData(form)
  });

  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`);
  }

  const payload = await response.json();
  renderRecommendations(payload);
  renderRecentSessions(payload);

  if (typeof onComplete === "function") {
    onComplete(payload);
  }
}

const preview = document.getElementById("camera-preview");
const canvas = document.getElementById("camera-canvas");
const startCameraButton = document.getElementById("start-camera-button");
const captureFrameButton = document.getElementById("capture-frame-button");
const captureDataUrl = document.getElementById("capture-data-url");

let detectionTimer = null;

async function requestServerEmotionPrediction(features) {
  try {
    const response = await fetch("/api/facial-emotion/predict", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(features)
    });

    if (!response.ok) {
      return null;
    }

    return await response.json();
  } catch (error) {
    console.error(error);
    return null;
  }
}

function mapDetectedExpression(expressions) {
  if (!expressions) {
    return null;
  }

  const sorted = Object.entries(expressions).sort((a, b) => b[1] - a[1]);
  const [label, score] = sorted[0] || [];
  if (!label || score < 0.45) {
    return null;
  }

  switch (label) {
    case "happy":
      return "Happy";
    case "sad":
      return "Sad";
    case "angry":
      return "Fearless";
    case "surprised":
      return "Excited";
    case "fearful":
      return "Fearless";
    case "disgusted":
      return "Curious";
    case "neutral":
      return "Relaxed";
    default:
      return "Curious";
  }
}

function distance(pointA, pointB) {
  return Math.hypot(pointA.x - pointB.x, pointA.y - pointB.y);
}

function averagePoint(points) {
  return {
    x: points.reduce((sum, point) => sum + point.x, 0) / points.length,
    y: points.reduce((sum, point) => sum + point.y, 0) / points.length
  };
}

function eyeOpennessRatio(eyePoints) {
  const vertical = distance(eyePoints[1], eyePoints[5]) + distance(eyePoints[2], eyePoints[4]);
  const horizontal = Math.max(distance(eyePoints[0], eyePoints[3]) * 2, 0.0001);
  return vertical / horizontal;
}

async function startAutoDetection() {
  if (!window.faceapi || !preview) {
    if (sourceLabel) {
      sourceLabel.textContent = "Automatic detector unavailable. Use the manual mood section instead.";
    }
    return;
  }

  try {
    await faceapi.nets.tinyFaceDetector.loadFromUri("https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model/");
    await faceapi.nets.faceExpressionNet.loadFromUri("https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model/");
    await faceapi.nets.faceLandmark68TinyNet.loadFromUri("https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model/");
  } catch (error) {
    console.error(error);
    if (sourceLabel) {
      sourceLabel.textContent = "Could not load the facial emotion model. Use the manual mood section instead.";
    }
    return;
  }

  if (detectionTimer) {
    clearInterval(detectionTimer);
  }

  detectionTimer = window.setInterval(async () => {
    if (!preview.srcObject) {
      return;
    }

    try {
      const result = await faceapi
        .detectSingleFace(preview, new faceapi.TinyFaceDetectorOptions())
        .withFaceLandmarks(true)
        .withFaceExpressions();

      const expressionMap = result?.expressions;
      const detectedEmotion = mapDetectedExpression(expressionMap);
      if (!detectedEmotion || !result?.detection?.box || !result?.landmarks) {
        return;
      }

      const box = result.detection.box;
      const landmarks = result.landmarks;
      const mouth = landmarks.getMouth();
      const leftEye = landmarks.getLeftEye();
      const rightEye = landmarks.getRightEye();
      const leftBrow = landmarks.getLeftEyeBrow();
      const rightBrow = landmarks.getRightEyeBrow();
      const faceWidth = Math.max(box.width, 1);
      const faceHeight = Math.max(box.height, 1);
      const mouthLeft = mouth[0];
      const mouthRight = mouth[6];
      const upperLip = averagePoint([mouth[13], mouth[14], mouth[15]]);
      const lowerLip = averagePoint([mouth[17], mouth[18], mouth[19]]);
      const leftEyeCenter = averagePoint(leftEye);
      const rightEyeCenter = averagePoint(rightEye);
      const leftBrowCenter = averagePoint(leftBrow);
      const rightBrowCenter = averagePoint(rightBrow);
      const avgEyeOpenness = (eyeOpennessRatio(leftEye) + eyeOpennessRatio(rightEye)) / 2;
      const browRaiseDistance = ((leftEyeCenter.y - leftBrowCenter.y) + (rightEyeCenter.y - rightBrowCenter.y)) / 2;
      const smileWidth = distance(mouthLeft, mouthRight) / faceWidth;
      const mouthHeight = distance(upperLip, lowerLip) / faceHeight;
      const normalizedEyeOpen = Math.min(1, Math.max(0, avgEyeOpenness / 0.42));
      const normalizedBrowRaise = Math.min(1, Math.max(0, browRaiseDistance / (faceHeight * 0.16)));
      const cheekLiftEstimate = Math.min(1, Math.max(0, (expressionMap.happy ?? 0) * 0.6 + (0.55 - avgEyeOpenness) * 0.7));
      const jawDropEstimate = Math.min(1, Math.max(0, mouthHeight * 3.8));
      const browTensionEstimate = Math.min(1, Math.max(0, (expressionMap.angry ?? 0) * 0.6 + (expressionMap.sad ?? 0) * 0.35 + Math.max(0, 0.34 - normalizedEyeOpen)));

      const features = {
        smileScore: expressionMap.happy ?? 0,
        browTension: browTensionEstimate,
        eyeOpenness: Math.max(expressionMap.surprised ?? 0, expressionMap.fearful ?? 0, normalizedEyeOpen),
        mouthWidthRatio: Math.min(1, Math.max(0, smileWidth * 1.7)),
        mouthHeightRatio: Math.min(1, Math.max(0, mouthHeight * 4.5)),
        browRaiseRatio: normalizedBrowRaise,
        eyeOpenRatio: normalizedEyeOpen,
        cheekLiftRatio: cheekLiftEstimate,
        jawDropRatio: jawDropEstimate
      };

      const serverPrediction = await requestServerEmotionPrediction(features);
      const finalEmotion = serverPrediction?.emotion || detectedEmotion;
      const confidenceNote = serverPrediction?.confidence ? `, confidence ${(serverPrediction.confidence * 100).toFixed(0)}%` : "";
      const source = serverPrediction?.source || `Auto-detected from facial expression (${Object.entries(expressionMap).sort((a, b) => b[1] - a[1])[0][0]}${confidenceNote})`;

      setEmotion(finalEmotion, source);
    } catch (error) {
      console.error(error);
    }
  }, 1600);
}

if (manualRecommendForm) {
  manualRecommendForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (manualEmotionSelect) {
      setEmotion(manualEmotionSelect.value, "Manual mood selection");
    }

    try {
      await postFormInPlace(manualRecommendForm, (payload) => {
        if (payload?.selectedEmotion) {
          setEmotion(payload.selectedEmotion, "Manual mood selection");
        }
      });
    } catch (error) {
      console.error(error);
      manualRecommendForm.submit();
    }
  });
}

if (manualEmotionSelect) {
  manualEmotionSelect.addEventListener("change", () => {
    setEmotion(manualEmotionSelect.value, "Manual mood selection");
  });
}

if (cameraForm) {
  cameraForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    syncCameraGenreFilters();

    try {
      await postFormInPlace(cameraForm, (payload) => {
        if (payload?.selectedEmotion) {
          setEmotion(payload.selectedEmotion, sourceInput?.value || "Camera capture");
        }
      });
    } catch (error) {
      console.error(error);
      cameraForm.submit();
    }
  });
}

if (preview && startCameraButton) {
  startCameraButton.addEventListener("click", async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
      preview.srcObject = stream;
      if (sourceLabel) {
        sourceLabel.textContent = "Camera live. Looking for your current facial expression.";
      }
      await startAutoDetection();
    } catch (error) {
      if (emotionLabel) {
        emotionLabel.textContent = "Camera access blocked";
      }
      if (sourceLabel) {
        sourceLabel.textContent = "Camera access was blocked. Use the manual mood section instead.";
      }
      console.error(error);
    }
  });
}

if (preview && canvas && captureFrameButton && captureDataUrl) {
  captureFrameButton.addEventListener("click", () => {
    syncCameraGenreFilters();
    const width = preview.videoWidth || 640;
    const height = preview.videoHeight || 360;
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) {
      return;
    }

    context.drawImage(preview, 0, 0, width, height);
    captureDataUrl.value = canvas.toDataURL("image/png");
    if (sourceLabel && emotionInput?.value) {
      sourceLabel.textContent = `${sourceInput?.value || "Detected mood"} saved from the latest frame.`;
    }
  });
}
