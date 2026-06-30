using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class IntroManager : MonoBehaviour
{
    [Header("Pantallas")]
    public GameObject pantallaInicio;
    public GameObject pantallaTutorial;
    public GameObject pantallaDisclaimer;

    [Header("Barras de carga")]
    public Image barraInicio;
    public Image barraTutorial;

    [Header("Escena del juego")]
    public string nombreEscenaJuego = "Field Unlocked";

    private int estadoActual = 0;

    void Start()
    {
        pantallaInicio.SetActive(true);
        pantallaTutorial.SetActive(false);
        pantallaDisclaimer.SetActive(false);

        if (barraInicio != null)
            StartCoroutine(AnimarBarra(barraInicio));
    }

    void Update()
    {
        bool presiono = false;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            presiono = true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            presiono = true;

        if (presiono)
            AvanzarPantalla();
    }

    void AvanzarPantalla()
    {
        if (estadoActual == 0)
        {
            estadoActual = 1;
            pantallaInicio.SetActive(false);
            pantallaTutorial.SetActive(true);
            if (barraTutorial != null)
                StartCoroutine(AnimarBarra(barraTutorial));
        }
        else if (estadoActual == 1)
        {
            estadoActual = 2;
            pantallaTutorial.SetActive(false);
            pantallaDisclaimer.SetActive(true);
        }
        else if (estadoActual == 2)
        {
            SceneManager.LoadScene(nombreEscenaJuego);
        }
    }

    IEnumerator AnimarBarra(Image barra)
    {
        barra.fillAmount = 0f;
        float duracion = 3f;
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            barra.fillAmount = tiempo / duracion;
            yield return null;
        }
        barra.fillAmount = 1f;
    }
}