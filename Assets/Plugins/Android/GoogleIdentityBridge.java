package com.gaeggul.identity;

import android.app.Activity;
import android.os.CancellationSignal;
import androidx.annotation.Keep;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;
import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import java.util.concurrent.Executor;

/** Credential Manager bridge. Tokens stay in memory and are never logged or persisted. */
@Keep public final class GoogleIdentityBridge {
    @Keep
    public interface Callback {
        void onSuccess(String token);
        void onError(String reason);
    }
    @Keep public static final class Request {
        private final CancellationSignal cancellation = new CancellationSignal();
        public void cancel() { cancellation.cancel(); }
    }
    public static Request signIn(Activity activity, String clientId, String nonce, boolean interactive, Callback callback) {
        Request operation = new Request();
        activity.runOnUiThread(() -> {
            try {
                GetCredentialRequest.Builder builder = new GetCredentialRequest.Builder();
                if (interactive) {
                    builder.addCredentialOption(new GetSignInWithGoogleOption.Builder(clientId).setNonce(nonce).build());
                } else {
                    builder.addCredentialOption(new GetGoogleIdOption.Builder().setServerClientId(clientId)
                        .setNonce(nonce).setFilterByAuthorizedAccounts(true).setAutoSelectEnabled(true).build());
                }
                Executor executor = command -> activity.runOnUiThread(command);
                CredentialManager.create(activity).getCredentialAsync(activity, builder.build(), operation.cancellation, executor,
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override public void onResult(GetCredentialResponse response) {
                            try {
                                GoogleIdTokenCredential credential = GoogleIdTokenCredential.createFrom(response.getCredential().getData());
                                callback.onSuccess(credential.getIdToken());
                            } catch (Exception error) { callback.onError("invalid_credential"); }
                        }
                        @Override public void onError(GetCredentialException error) { callback.onError(error.getType()); }
                    });
            } catch (Exception error) { callback.onError("credential_unavailable"); }
        });
        return operation;
    }
}
